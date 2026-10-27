using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

namespace Nullable.Extended.Mcp;

/// <summary>
/// Checks which null-forgiving operators are required, by compiling the owning project; this is the expensive second tier.
/// </summary>
/// <remarks>
/// A project is loaded on demand, in the background, with all its target frameworks, and then kept. For every state of the project (a <see cref="Generation"/>)
/// there is one <see cref="AnalysisSession"/> that is initially empty and gets the documents that are requested, so only these are post-processed,
/// and the expensive baseline of the project is calculated once. Tool calls never block on this work for longer than the requested time; results that
/// are not ready yet are reported as pending. Results are only valid for the code they have been calculated for: if a source file of the project changes,
/// all results of the project are dropped and a new generation is started on a new snapshot (<see cref="Solution.WithDocumentText"/>, which never writes to disk);
/// if the project file or a reference changes, e.g. because the solution has been built, the project is loaded again.
/// </remarks>
internal sealed class RequiredChecker(IAnalyzerEngine analyzerEngine, ServerOptions options, ILogger<RequiredChecker> logger) : IAsyncDisposable
{
    public const int DefaultWaitSeconds = 20;
    public const int MaxWaitSeconds = 60;

    private const int MaxLoadedProjects = 4;
    private const int MaxMessages = 10;
    private const int MaxReportedItems = 500;
    private const int MaxReportedErrors = 3;
    private const int MaxMessageLength = 300;

    private static readonly IEqualityComparer<string?> PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // Errors that are caused by references that are not available, which typically means that the solution has not been built.
    private static readonly HashSet<string> MissingReferenceErrors = new(StringComparer.OrdinalIgnoreCase) { "CS0006", "CS0012", "CS0234", "CS0246", "CS0400", "CS0518", "CS1705" };

    private readonly ConcurrentDictionary<string, ProjectEntry> _entries = new(PathComparer);
    private readonly CancellationTokenSource _shutdown = new();

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        await ResetAsync();
    }

    /// <summary>
    /// Drops all loaded projects and their results.
    /// </summary>
    public async Task ResetAsync()
    {
        foreach (var (key, entry) in _entries.ToArray())
        {
            await RemoveAsync(key, entry);
        }
    }

    /// <summary>
    /// Checks the files, each belonging to a project. Waits for up to <paramref name="waitSeconds"/> for the results; what is not ready is reported as pending.
    /// </summary>
    public async Task<CheckResult> CheckAsync(IReadOnlyList<SourceFile> files, int waitSeconds, CancellationToken cancellationToken)
    {
        if (files.Count > options.MaxCheckFiles)
            throw new McpException($"{files.Count} files would have to be checked, but a check is limited to {options.MaxCheckFiles} files per call, because every file needs a semantic analysis. Narrow the scope, or check in several calls.");

        var clock = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(waitSeconds, 0, MaxWaitSeconds));

        var groups = files.GroupBy(file => file.ProjectPath!, PathComparer).ToArray();
        var keep = groups.Select(group => group.Key).ToHashSet(PathComparer);

        var outcomes = await Task.WhenAll(groups.Select(group => CheckProjectAsync(group.Key, group.ToArray(), clock, timeout, keep, cancellationToken)));

        var items = outcomes.SelectMany(outcome => outcome.Items).OrderBy(item => item.File, StringComparer.Ordinal).ThenBy(item => item.Line).ThenBy(item => item.Column).ToArray();
        var problems = outcomes.SelectMany(outcome => outcome.Problems).ToArray();
        var projects = outcomes.Select(outcome => outcome.Project).ToArray();

        var counts = new RequiredCounts(
            items.Count(item => item.Required == RequiredStates.Yes),
            items.Count(item => item.Required == RequiredStates.No),
            items.Count(item => item.Required == RequiredStates.Unknown),
            items.Count(item => item.Required == RequiredStates.Pending),
            0);

        var complete = counts.Pending == 0 && problems.All(problem => problem.State != "pending");
        var hints = new List<string>();

        if (!complete)
        {
            hints.Add("Some results are still being calculated in the background; call check_required again with the same arguments. Loading and compiling a large project can take a minute.");
        }

        if (projects.Any(project => project.BuildRecommended))
        {
            hints.Add("References are missing, or errors are caused by missing references: build the solution (all target frameworks) and check again; the project is loaded again automatically.");
        }

        return new CheckResult(complete, hints.Count > 0 ? string.Join(" ", hints) : null, counts, items.Length, projects, problems, items.Take(MaxReportedItems).ToArray());
    }

    /// <summary>
    /// The states of the operators of already checked files whose results are still valid, by occurrence id; does not load or check anything.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetKnownStates(IEnumerable<SourceFile> files)
    {
        var states = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in files.Where(file => file.ProjectPath != null).GroupBy(file => file.ProjectPath!, PathComparer))
        {
            if (!_entries.TryGetValue(group.Key, out var entry) || entry.Load is not { IsCompletedSuccessfully: true } load)
                continue;

            var loaded = load.Result;
            var generation = loaded.Current;

            if (generation.IsCancelled || Classify(loaded, out _) != ProjectChange.None)
                continue;

            var resultsByFile = generation.Session.Results.OfType<NullForgivingAnalysisResult>().ToLookup(result => result.FilePath, PathComparer);

            foreach (var file in group)
            {
                if (!generation.Checked.ContainsKey(file.FullPath))
                    continue;

                foreach (var result in resultsByFile[file.FullPath])
                {
                    states[$"{file.RelativePath}:{result.Line}:{result.Column}"] = Evaluate(result);
                }
            }
        }

        return states;
    }

    private async Task<ProjectOutcome> CheckProjectAsync(string projectPath, IReadOnlyList<SourceFile> files, Stopwatch clock, TimeSpan timeout, ISet<string> keep, CancellationToken cancellationToken)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var projectFile = files[0].ProjectRelativePath;
        var entry = _entries.GetOrAdd(projectPath, _ => new ProjectEntry());

        TimeSpan Remaining() => timeout > clock.Elapsed ? timeout - clock.Elapsed : TimeSpan.Zero;

        ProjectOutcome Pending(string state, string message, IReadOnlyList<string>? messages = null) => new(
            new ProjectCheck(projectName, projectFile, state, [], messages ?? [], false),
            [],
            files.Select(file => new FileProblem(file.RelativePath, state == "failed" ? "rejected" : "pending", message)).ToArray());

        Task<LoadedProject> loadTask;

        await entry.Gate.WaitAsync(cancellationToken);

        try
        {
            loadTask = PrepareLocked(entry, projectPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cannot prepare project '{Project}'", projectPath);

            return Pending("failed", $"The project could not be prepared: {ex.Message}");
        }
        finally
        {
            entry.Gate.Release();
        }

        EvictIdle(keep);

        LoadedProject loaded;

        try
        {
            if (!await TryWaitAsync(loadTask, Remaining(), cancellationToken))
                return Pending("loading", "The project is being loaded.");

            loaded = await loadTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            entry.FaultReported = true;

            return Pending("loading", "Loading the project has been cancelled, because the server has been reloaded; call again.");
        }
        catch (Exception ex)
        {
            entry.FaultReported = true;

            logger.LogWarning("Cannot load project '{Project}': {Message}", projectPath, ex.Message);

            return Pending("failed", $"The project could not be loaded: {ex.Message}", (ex as ProjectLoadException)?.Messages);
        }

        var generation = loaded.Current;
        var problems = new List<FileProblem>();
        var contexts = new List<DocumentContext>();

        await generation.Queue.WaitAsync(cancellationToken);

        try
        {
            foreach (var file in files)
            {
                if (!generation.Checked.TryGetValue(file.FullPath, out var fileContexts))
                {
                    if (!loaded.DocumentIds.TryGetValue(file.FullPath, out var documentIds))
                    {
                        problems.Add(new FileProblem(file.RelativePath, "rejected", "The file is not part of the project as it has been loaded. If the file is new, or the project file has changed, call reload."));
                        continue;
                    }

                    var created = new List<DocumentContext>();

                    foreach (var documentId in documentIds)
                    {
                        var document = generation.Solution.GetDocument(documentId)!;

                        if (await generation.Session.UpdateAsync(document, generation.Token) is { } context)
                        {
                            created.Add(context);
                        }
                    }

                    fileContexts = generation.Checked[file.FullPath] = created;
                }

                contexts.AddRange(fileContexts);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Pending("loading", "The project has changed while it was checked; call again.");
        }
        finally
        {
            generation.Queue.Release();
        }

        try
        {
            await TryWaitAsync(Task.WhenAll(contexts.Select(context => context.Completion)), Remaining(), cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The generation has been cancelled by a change or a reload, this is handled below.
        }

        var rejected = problems.Select(problem => problem.File).ToHashSet(StringComparer.Ordinal);

        if (generation.IsCancelled)
        {
            problems.AddRange(files.Where(file => !rejected.Contains(file.RelativePath)).Select(file => new FileProblem(file.RelativePath, "pending", "The project has changed while it was checked; call again.")));

            return new ProjectOutcome(CreateProjectCheck(loaded, projectName, projectFile, false), [], problems);
        }

        var results = generation.Session.Results.OfType<NullForgivingAnalysisResult>().ToLookup(result => result.FilePath, PathComparer);
        var items = new List<RequiredItem>();
        var buildRecommended = loaded.MissingReferences.Count > 0;

        foreach (var file in files.Where(file => !rejected.Contains(file.RelativePath)))
        {
            string? errors = null;
            var errorsDescribed = false;

            foreach (var result in results[file.FullPath].OrderBy(result => result.Line).ThenBy(result => result.Column))
            {
                var state = Evaluate(result);
                string? reason = null;

                if (state == RequiredStates.Unknown)
                {
                    if (result.HasCompilationErrors)
                    {
                        if (!errorsDescribed)
                        {
                            (errors, var causedByMissingReferences) = await DescribeErrorsAsync(result.Variants.First(variant => variant.HasCompilationErrors).DocumentContext, cancellationToken);
                            errorsDescribed = true;
                            buildRecommended |= causedByMissingReferences;
                        }

                        reason = "The file has compile errors, so it can't be evaluated" + (errors != null ? ": " + errors : ".");
                    }
                    else
                    {
                        reason = "The evaluation did not complete.";
                    }
                }

                items.Add(new RequiredItem(
                    $"{file.RelativePath}:{result.Line}:{result.Column}",
                    file.RelativePath,
                    result.Line,
                    result.Column,
                    NullForgivingScanner.GetStatus(result.Justification),
                    result.Justification,
                    state,
                    reason));
            }
        }

        // Files without operators have no documents to post-process, so a file that is still being analyzed always has items, whose state is pending.
        return new ProjectOutcome(CreateProjectCheck(loaded, projectName, projectFile, buildRecommended), items, problems);
    }

    private static ProjectCheck CreateProjectCheck(LoadedProject loaded, string projectName, string? projectFile, bool buildRecommended)
    {
        var messages = loaded.Messages.ToList();

        if (loaded.MissingReferences.Count > 0)
        {
            messages.Add($"{loaded.MissingReferences.Count} referenced file(s) do not exist, e.g. '{loaded.MissingReferences[0]}'.");
        }

        return new ProjectCheck(projectName, projectFile, "ready", loaded.TargetFrameworks, messages, buildRecommended);
    }

    private static async Task<bool> TryWaitAsync(Task task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(timeout, cancellationToken);

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the load task of the project, and starts a new one, or a new generation, if the project has changed. Must be called with the gate of the entry held.
    /// </summary>
    private Task<LoadedProject> PrepareLocked(ProjectEntry entry, string projectPath)
    {
        entry.LastUsedTicks = DateTime.UtcNow.Ticks;

        var load = entry.Load;

        if (load != null)
        {
            if (!load.IsCompleted)
                return load;

            if (!load.IsCompletedSuccessfully)
            {
                // A failure is reported once, before the next call tries again.
                if (!entry.FaultReported)
                    return load;
            }
            else
            {
                var loaded = load.Result;

                switch (Classify(loaded, out var changedSources))
                {
                    case ProjectChange.None:
                        return load;

                    case ProjectChange.Sources when TryApplySourceChanges(loaded, changedSources):
                        return load;
                }

                logger.LogInformation("Project '{Project}' has changed, loading it again", projectPath);
                loaded.Dispose();
            }
        }

        entry.FaultReported = false;
        entry.Cts?.Cancel();
        entry.Cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);

        var token = entry.Cts.Token;

        return entry.Load = Task.Run(() => LoadAsync(projectPath, token), token);
    }

    private async Task<LoadedProject> LoadAsync(string projectPath, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var messages = new List<string>();
        var workspace = MSBuildWorkspace.Create();

        workspace.LoadMetadataForReferencedProjects = true;
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            lock (messages)
            {
                if (messages.Count < MaxMessages)
                {
                    messages.Add($"{args.Diagnostic.Kind}: {Shorten(args.Diagnostic.Message)}");
                }
            }
        });

        try
        {
            await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var projects = workspace.CurrentSolution.Projects.Where(project => PathComparer.Equals(project.FilePath, projectPath)).ToArray();

            if (projects.Length == 0)
                throw new InvalidOperationException("The project file did not result in a C# project.");

            var documentIds = new Dictionary<string, List<DocumentId>>(PathComparer);
            var inputStamps = new Dictionary<string, FileStamp?>(PathComparer) { [projectPath] = FileStamp.TryGet(projectPath) };
            var sourceStamps = new Dictionary<string, FileStamp?>(PathComparer);

            foreach (var project in projects)
            {
                foreach (var document in project.Documents.Where(document => document.FilePath != null))
                {
                    if (!documentIds.TryGetValue(document.FilePath!, out var ids))
                    {
                        documentIds[document.FilePath!] = ids = [];
                        sourceStamps[document.FilePath!] = FileStamp.TryGet(document.FilePath!);
                    }

                    ids.Add(document.Id);
                }

                foreach (var reference in project.MetadataReferences.OfType<PortableExecutableReference>().Where(reference => reference.FilePath != null))
                {
                    inputStamps.TryAdd(reference.FilePath!, FileStamp.TryGet(reference.FilePath!));
                }
            }

            var targetFrameworks = projects
                .Select(project => project.Name)
                .Where(name => name.EndsWith(')') && name.Contains('('))
                .Select(name => name.Substring(name.LastIndexOf('(') + 1).TrimEnd(')'))
                .ToArray();

            var missingReferences = inputStamps.Where(pair => pair.Value == null && !PathComparer.Equals(pair.Key, projectPath)).Select(pair => pair.Key).Order().ToArray();

            var loaded = new LoadedProject
            {
                Workspace = workspace,
                TargetFrameworks = targetFrameworks,
                Messages = messages.ToArray(),
                MissingReferences = missingReferences,
                DocumentIds = documentIds.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<DocumentId>)pair.Value, PathComparer),
                InputStamps = inputStamps,
                Current = CreateGeneration(workspace.CurrentSolution, sourceStamps)
            };

            logger.LogInformation("Loaded project '{Project}' in {Elapsed} ms: {Frameworks} target framework(s), {Documents} documents, {Missing} missing references",
                projectPath, stopwatch.ElapsedMilliseconds, projects.Length, documentIds.Count, missingReferences.Length);

            return loaded;
        }
        catch (OperationCanceledException)
        {
            workspace.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            workspace.Dispose();

            string[] loadMessages;

            lock (messages)
            {
                loadMessages = messages.ToArray();
            }

            throw new ProjectLoadException(ex.Message, loadMessages);
        }
    }

    private Generation CreateGeneration(Solution solution, IReadOnlyDictionary<string, FileStamp?> sourceStamps)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var session = analyzerEngine.CreateSession([], true, cts.Token);

        // The session has no documents to analyze, it only has to be started before documents can be added.
        _ = session.StartAsync();

        return new Generation(solution, session, cts, sourceStamps);
    }

    private enum ProjectChange
    {
        None,
        Sources,
        Project
    }

    /// <summary>
    /// Checks the files the loaded project is based on. A change of the project or its references requires to load it again, a change of source files a new generation.
    /// </summary>
    private static ProjectChange Classify(LoadedProject loaded, out IReadOnlyList<string> changedSources)
    {
        var changed = new List<string>();

        changedSources = changed;

        foreach (var (path, stamp) in loaded.InputStamps)
        {
            if (FileStamp.TryGet(path) != stamp)
                return ProjectChange.Project;
        }

        foreach (var (path, stamp) in loaded.Current.SourceStamps)
        {
            if (FileStamp.TryGet(path) != stamp)
            {
                changed.Add(path);
            }
        }

        return changed.Count > 0 ? ProjectChange.Sources : ProjectChange.None;
    }

    /// <summary>
    /// Starts a new generation with the changed source files, based on a new snapshot of the solution; nothing is written to disk.
    /// Returns false if the files could not be read, so the project has to be loaded again.
    /// </summary>
    private bool TryApplySourceChanges(LoadedProject loaded, IReadOnlyList<string> changedSources)
    {
        var current = loaded.Current;
        var solution = current.Solution;
        var stamps = new Dictionary<string, FileStamp?>(current.SourceStamps, PathComparer);

        try
        {
            foreach (var path in changedSources)
            {
                // The stamp is taken before the file is read, so a change while reading is detected by the next call.
                var stamp = FileStamp.TryGet(path);

                if (stamp == null)
                    return false;

                SourceText text;

                using (var stream = File.OpenRead(path))
                {
                    text = SourceText.From(stream);
                }

                foreach (var documentId in loaded.DocumentIds[path])
                {
                    solution = solution.WithDocumentText(documentId, text);
                }

                stamps[path] = stamp;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        logger.LogInformation("{Count} source file(s) of the project have changed, results are dropped", changedSources.Count);

        loaded.Current = CreateGeneration(solution, stamps);
        current.Cancel();

        return true;
    }

    private static string Evaluate(NullForgivingAnalysisResult result)
    {
        if (result.IsRequired == true)
            return RequiredStates.Yes;

        if (result.Variants.Any(variant => !variant.DocumentContext.Completion.IsCompleted))
            return RequiredStates.Pending;

        return result.IsRequired == false ? RequiredStates.No : RequiredStates.Unknown;
    }

    private static async Task<(string? Description, bool CausedByMissingReferences)> DescribeErrorsAsync(DocumentContext context, CancellationToken cancellationToken)
    {
        try
        {
            var model = await context.Document.GetSemanticModelAsync(cancellationToken);

            if (model == null)
                return (null, false);

            var errors = model.GetDiagnostics(null, cancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !diagnostic.IsSuppressed && !IsNullableDiagnostic(diagnostic.Id))
                .ToArray();

            var description = string.Join("; ", errors
                .Take(MaxReportedErrors)
                .Select(diagnostic => $"{diagnostic.Id} (line {diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}) {Shorten(diagnostic.GetMessage())}"));

            return (description.Length > 0 ? description : null, errors.Any(diagnostic => MissingReferenceErrors.Contains(diagnostic.Id)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, false);
        }
    }

    // The nullable warnings CS8600 - CS8900 are not what makes a file fail.
    private static bool IsNullableDiagnostic(string id)
    {
        return string.Compare(id, "CS8600", StringComparison.OrdinalIgnoreCase) >= 0
               && string.Compare(id, "CS8900", StringComparison.OrdinalIgnoreCase) <= 0;
    }

    private static string Shorten(string message)
    {
        message = message.ReplaceLineEndings(" ");

        return message.Length > MaxMessageLength ? message.Substring(0, MaxMessageLength) + "…" : message;
    }

    /// <summary>
    /// Unloads the projects that have not been used for the longest time, if too many are loaded; a loaded project needs a lot of memory.
    /// </summary>
    private void EvictIdle(ISet<string> keep)
    {
        var excess = _entries.Count(pair => pair.Value.Load != null) - MaxLoadedProjects;

        if (excess <= 0)
            return;

        var candidates = _entries
            .Where(pair => !keep.Contains(pair.Key) && pair.Value.Load is { IsCompleted: true })
            .OrderBy(pair => pair.Value.LastUsedTicks)
            .Take(excess)
            .ToArray();

        foreach (var (key, entry) in candidates)
        {
            logger.LogInformation("Unloading project '{Project}', too many projects are loaded", key);

            _ = RemoveAsync(key, entry);
        }
    }

    private async Task RemoveAsync(string key, ProjectEntry entry)
    {
        await entry.Gate.WaitAsync();

        try
        {
            if (!_entries.TryRemove(new KeyValuePair<string, ProjectEntry>(key, entry)))
                return;

            entry.Cts?.Cancel();

            if (entry.Load is { } load)
            {
                // A load that is still running is cancelled, but has to dispose its workspace when it is done.
                _ = load.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        task.Result.Dispose();
                    }
                }, TaskScheduler.Default);
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc)
    {
        public static FileStamp? TryGet(string path)
        {
            try
            {
                var info = new FileInfo(path);

                return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    private sealed class ProjectEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public Task<LoadedProject>? Load { get; set; }

        public CancellationTokenSource? Cts { get; set; }

        public volatile bool FaultReported;

        public long LastUsedTicks;
    }

    /// <summary>
    /// A project as loaded by MSBuild, with all its target frameworks.
    /// </summary>
    private sealed class LoadedProject : IDisposable
    {
        public required MSBuildWorkspace Workspace { get; init; }

        public required IReadOnlyList<string> TargetFrameworks { get; init; }

        public required IReadOnlyList<string> Messages { get; init; }

        public required IReadOnlyList<string> MissingReferences { get; init; }

        /// <summary>The documents of a source file, one for every target framework.</summary>
        public required IReadOnlyDictionary<string, IReadOnlyList<DocumentId>> DocumentIds { get; init; }

        /// <summary>The project file and all referenced files; if one of them changes, the project has to be loaded again.</summary>
        public required IReadOnlyDictionary<string, FileStamp?> InputStamps { get; init; }

        private volatile Generation? _current;

        public required Generation Current
        {
            get => _current!;
            set => _current = value;
        }

        public void Dispose()
        {
            Current.Cancel();
            Workspace.Dispose();
        }
    }

    /// <summary>
    /// The state of the source files of a project, with the session that analyzes them. Results are only valid within their generation.
    /// </summary>
    private sealed class Generation(Solution solution, AnalysisSession session, CancellationTokenSource cancellation, IReadOnlyDictionary<string, FileStamp?> sourceStamps)
    {
        public Solution Solution => solution;

        public AnalysisSession Session => session;

        public CancellationToken Token => cancellation.Token;

        public bool IsCancelled => cancellation.IsCancellationRequested;

        /// <summary>The source files this generation is based on.</summary>
        public IReadOnlyDictionary<string, FileStamp?> SourceStamps => sourceStamps;

        /// <summary>Serializes adding documents to the session.</summary>
        public SemaphoreSlim Queue { get; } = new(1, 1);

        /// <summary>The documents that have been added to the session, by file.</summary>
        public ConcurrentDictionary<string, IReadOnlyList<DocumentContext>> Checked { get; } = new(PathComparer);

        public void Cancel() => cancellation.Cancel();
    }

    private sealed record ProjectOutcome(ProjectCheck Project, IReadOnlyList<RequiredItem> Items, IReadOnlyList<FileProblem> Problems);

    private sealed class ProjectLoadException(string message, IReadOnlyList<string> messages) : Exception(message)
    {
        public IReadOnlyList<string> Messages => messages;
    }
}
