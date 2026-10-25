using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    /// <summary>
    /// An analysis of a set of documents. <see cref="StartAsync"/> analyzes the documents and then post-processes the results in the background.
    /// Contains the context of every analyzed document, each with its analysis results.
    /// Also caches the project contexts, so they are calculated on demand, but only once per project snapshot.
    /// All documents the session is created with must originate from the same solution snapshot; documents that are updated later may come from a newer snapshot.
    /// </summary>
    public sealed class AnalysisSession
    {
        private readonly ConcurrentDictionary<Project, Lazy<Task<ProjectContext>>> _projectContexts = new();
        private readonly IReadOnlyCollection<Document> _documents;
        private readonly IReadOnlyCollection<ISyntaxTreeAnalyzer> _syntaxTreeAnalyzers;
        private readonly IReadOnlyCollection<ISyntaxAnalysisPostProcessor> _postProcessors;
        private readonly object _startLock = new();
        private readonly object _resultsLock = new();

        // The ticket and the context of a document are changed together by atomically swapping the whole map, so no lock is needed.
        private ImmutableDictionary<DocumentId, DocumentState> _documentStates = ImmutableDictionary<DocumentId, DocumentState>.Empty;
        private volatile IReadOnlyCollection<AnalysisResult> _results = [];
        private Task? _startTask;

        internal AnalysisSession(IReadOnlyCollection<Document> documents, IReadOnlyCollection<ISyntaxTreeAnalyzer> syntaxTreeAnalyzers,
            IReadOnlyCollection<ISyntaxAnalysisPostProcessor> postProcessors, CancellationToken cancellationToken)
        {
            _documents = documents;
            _syntaxTreeAnalyzers = syntaxTreeAnalyzers;
            _postProcessors = postProcessors;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Analyzes the documents. When the returned task completes, the <see cref="DocumentContexts"/> contain the analysis results,
        /// and the post-processing of all documents that have results is running in the background; use <see cref="Completion"/> to wait for it.
        /// Calling this more than once returns the same task.
        /// </summary>
        public Task StartAsync()
        {
            lock (_startLock)
            {
                return _startTask ??= Task.Run(AnalyzeDocumentsAsync, CancellationToken);
            }
        }

        /// <summary>
        /// The contexts of all analyzed documents; use <see cref="DocumentContext.Cancel"/> to cancel the post-processing of a single document.
        /// This is a snapshot, it does not reflect later calls to <see cref="UpdateAsync"/>, and it is empty until <see cref="StartAsync"/> has completed.
        /// </summary>
        public IReadOnlyCollection<DocumentContext> DocumentContexts => CurrentStates.Values
            .Select(state => state.Context)
            .OfType<DocumentContext>()
            .ToArray();

        /// <summary>
        /// Completes when the post-processing of all documents in <see cref="DocumentContexts"/> is done. Documents that have been cancelled individually are regarded as done.
        /// This only covers the documents at the time of the call, not later updates.
        /// </summary>
        public Task Completion => Task.WhenAll(DocumentContexts.Select(context => context.Completion));

        /// <summary>
        /// The analysis results of all documents, with only one result for every position in a file.
        /// A file can be part of several documents, e.g. when a project is built for several target frameworks, and the document results may differ between them.
        /// The document results at the same position are composed to a single result, which evaluates them as a whole;
        /// e.g. a null-forgiving operator is required if it is required in at least one of them.
        /// This is a snapshot, it does not reflect later calls to <see cref="UpdateAsync"/>, but the properties of the results are always up to date;
        /// results of unchanged positions are reused by later snapshots.
        /// </summary>
        public IReadOnlyCollection<AnalysisResult> Results => _results;

        private static IEnumerable<IReadOnlyList<DocumentAnalysisResult>> GroupDocumentResults(IEnumerable<DocumentContext> contexts)
        {
            // The order only determines which document result represents the group; make it independent of the order in which the documents have been analyzed.
            return contexts
                .SelectMany(context => context.Results)
                .GroupBy(result => (result.Position, ResultType: result.GetType()))
                .Select(group => (IReadOnlyList<DocumentAnalysisResult>)group
                    .OrderBy(result => result.DocumentContext.Document.Project.Name, StringComparer.Ordinal)
                    .ThenBy(result => result.DocumentContext.Document.Project.Id.Id)
                    .ToArray());
        }

        private void ComposeResults()
        {
            // The results must be composed of the current states, so the last one to get the lock wins.
            lock (_resultsLock)
            {
                var results = new List<AnalysisResult>();

                foreach (var documentResults in GroupDocumentResults(DocumentContexts))
                {
                    var result = documentResults[0].Result;

                    if (result == null || !result.Variants.SequenceEqual(documentResults))
                    {
                        result = documentResults[0].CreateResult(documentResults);

                        foreach (var documentResult in documentResults)
                        {
                            documentResult.Result = result;
                        }
                    }

                    results.Add(result);
                }

                _results = results.ToArray();
            }
        }

        /// <summary>
        /// Analyzes the updated document again. The existing context of the document is cancelled and replaced by a new one,
        /// and the post-processing of the new context starts immediately.
        /// If the initial analysis started by <see cref="StartAsync"/> is still running, this waits for it to complete first.
        /// </summary>
        /// <remarks>
        /// If the same document is updated again before this call has completed, the older call is superseded: it does not change the session and returns <c>null</c>.
        /// The calls are ordered by the time they are made, so a document must be updated in the order of its snapshots.
        /// </remarks>
        /// <returns>The new context, or <c>null</c> if the document does not need to be analyzed or the update has been superseded; in the first case the existing context is just removed.</returns>
        /// <exception cref="InvalidOperationException">The session has not been started.</exception>
        public async Task<DocumentContext?> UpdateAsync(Document document, CancellationToken cancellationToken)
        {
            var startTask = _startTask ?? throw new InvalidOperationException("The session has not been started.");

            // Take the ticket before the first await, so it reflects the order of the calls, not the order in which their analysis completes.
            var ticket = ImmutableInterlocked.AddOrUpdate(ref _documentStates, document.Id,
                _ => new DocumentState(1, null),
                (_, state) => new DocumentState(state.Ticket + 1, state.Context)).Ticket;

            await startTask.ConfigureAwait(false);

            // Cancel the existing context early. A state that still has our ticket cannot hold the context of a newer update, because that would have required a newer ticket.
            if (CurrentStates.TryGetValue(document.Id, out var currentState) && currentState.Ticket == ticket)
            {
                currentState.Context?.Cancel();
            }

            var newContext = await AnalyzeDocumentAsync(document, cancellationToken).ConfigureAwait(false);

            // Start the post-processing before the context is published, so its completion is already set when it becomes visible.
            // If the update turns out to be superseded, the context is cancelled again, so this costs nothing.
            if (newContext != null)
            {
                StartPostProcessing(newContext);
            }

            if (!TryReplaceContext(document.Id, ticket, newContext))
            {
                // A newer update of this document has been requested while this one was analyzed; its result would be outdated.
                newContext?.Cancel();
                return null;
            }

            RemoveUnusedProjectContexts();
            ComposeResults();

            return newContext;
        }

        private ImmutableDictionary<DocumentId, DocumentState> CurrentStates => Volatile.Read(ref _documentStates);

        /// <summary>
        /// Replaces the context of the document, and cancels the replaced context, unless a newer update has been requested in the meantime.
        /// </summary>
        private bool TryReplaceContext(DocumentId documentId, int ticket, DocumentContext? newContext)
        {
            while (true)
            {
                var states = CurrentStates;

                if (!states.TryGetValue(documentId, out var state) || state.Ticket != ticket)
                    return false;

                var newStates = states.SetItem(documentId, new DocumentState(ticket, newContext));

                // If another thread has changed the states in the meantime, try again with the new states, which will reveal whether we are still the latest update.
                if (Interlocked.CompareExchange(ref _documentStates, newStates, states) != states)
                    continue;

                state.Context?.Cancel();
                return true;
            }
        }

        /// <summary>
        /// The project contexts of project snapshots that are no longer used by any document will never be requested again.
        /// This is based on the states at the time of the call; should a concurrent update need a removed project context, it is just calculated again.
        /// </summary>
        private void RemoveUnusedProjectContexts()
        {
            var activeProjects = new HashSet<Project>(DocumentContexts.Select(context => context.Document.Project));

            foreach (var project in _projectContexts.Keys.Where(project => !activeProjects.Contains(project)))
            {
                _projectContexts.TryRemove(project, out _);
            }
        }

        private CancellationToken CancellationToken { get; }

        /// <summary>
        /// Limits the number of documents that are post processed concurrently, since post-processing is compilation heavy.
        /// </summary>
        private SemaphoreSlim PostProcessingSlots { get; } = new(Environment.ProcessorCount);

        private async Task AnalyzeDocumentsAsync()
        {
            var documentTasks = _documents.Select(document => AnalyzeDocumentAsync(document, CancellationToken));

            var analyzedContexts = await Task.WhenAll(documentTasks).ConfigureAwait(false);

            var contexts = analyzedContexts.OfType<DocumentContext>().ToList();

            foreach (var context in contexts)
            {
                StartPostProcessing(context);
            }

            ImmutableInterlocked.Update(ref _documentStates, AddAnalyzedContexts, contexts);

            ComposeResults();
        }

        // Updates that have been requested while the initial analysis was running have already taken their tickets; keep them.
        private static ImmutableDictionary<DocumentId, DocumentState> AddAnalyzedContexts(ImmutableDictionary<DocumentId, DocumentState> states, IEnumerable<DocumentContext> contexts)
        {
            return states.SetItems(contexts.Select(context =>
            {
                var documentId = context.Document.Id;
                var ticket = states.TryGetValue(documentId, out var state) ? state.Ticket : 0;

                return new KeyValuePair<DocumentId, DocumentState>(documentId, new DocumentState(ticket, context));
            }));
        }

        private Task<DocumentContext?> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                if (syntaxTree == null)
                    return null;

                var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);
                if (syntaxRoot.BeginsWithAutoGeneratedComment())
                    return null;

                var documentContext = new DocumentContext(document, syntaxTree, syntaxRoot);

                var tasks = _syntaxTreeAnalyzers
                    .Select(analyzer => analyzer.AnalyzeAsync(documentContext, cancellationToken));

                var results = await Task.WhenAll(tasks);

                // Only one result per position; if several analyzers report the same position, the first one wins.
                documentContext.Results = results
                    .SelectMany(r => r)
                    .GroupBy(r => r.Position)
                    .Select(g => g.First())
                    .ToArray();

                return documentContext;
            }, cancellationToken);
        }

        private void StartPostProcessing(DocumentContext context)
        {
            if (_postProcessors.Count > 0 && context.Results.Count > 0)
            {
                context.Completion = PostProcessDocumentAsync(context);
            }
        }

        /// <summary>
        /// Post processes the results of a single document. Cancelling the document only affects this document.
        /// </summary>
        private Task PostProcessDocumentAsync(DocumentContext context)
        {
            return Task.Run(async () =>
            {
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, context.CancellationToken);
                var cancellationToken = linkedTokenSource.Token;

                try
                {
                    var projectContext = await GetProjectContextAsync(context.Document.Project, cancellationToken);

                    if (projectContext.HasErrors(context.Document.FilePath))
                    {
                        MarkResultsAsHavingCompilationErrors(context);
                        return;
                    }

                    await PostProcessingSlots.WaitAsync(cancellationToken);

                    try
                    {
                        foreach (var postProcessor in _postProcessors)
                        {
                            await postProcessor.PostProcessAsync(context, projectContext, cancellationToken);
                        }
                    }
                    finally
                    {
                        PostProcessingSlots.Release();
                    }
                }
                catch (OperationCanceledException) when (context.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
                {
                    // Only this document was cancelled, e.g. because it needs to be re-evaluated; the results stay unprocessed.
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failing project or post processor must not break the analysis, the affected results are just marked.
                    MarkResultsAsHavingCompilationErrors(context);
                }
            }, CancellationToken);
        }

        private static void MarkResultsAsHavingCompilationErrors(DocumentContext context)
        {
            foreach (var result in context.Results)
            {
                result.MarkAsHavingCompilationErrors();
            }
        }

        /// <summary>
        /// Gets the context of the project. The calculation is shared by all documents of the project and is only bound to the
        /// cancellation of the session, so cancelling a single document will only stop waiting for the result, but not abort the shared calculation.
        /// </summary>
        private Task<ProjectContext> GetProjectContextAsync(Project project, CancellationToken cancellationToken)
        {
            // Avoid starting the expensive calculation for a document that is already cancelled.
            cancellationToken.ThrowIfCancellationRequested();

            var lazy = _projectContexts.GetOrAdd(project, _ => new Lazy<Task<ProjectContext>>(
                () => Task.Run(() => ProjectContext.CreateAsync(project, _postProcessors, CancellationToken), CancellationToken)));

            return WaitAsync(lazy.Value, cancellationToken);
        }

        private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled)
                return await task.ConfigureAwait(false);

            var cancellationTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancellationTask))
            {
                if (await Task.WhenAny(task, cancellationTask.Task).ConfigureAwait(false) != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// The ticket of the latest update that has been requested for a document, and the context that is currently installed for it.
        /// </summary>
        private readonly struct DocumentState(int ticket, DocumentContext? context)
        {
            public int Ticket { get; } = ticket;

            public DocumentContext? Context { get; } = context;
        }
    }
}
