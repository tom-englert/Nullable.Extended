using System.Collections.Concurrent;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;
using Nullable.Shared;

namespace Nullable.Extended.Mcp;

/// <summary>
/// Finds the null-forgiving operators by a purely syntactic analysis; no compilation, project loading or restore is needed.
/// The results are cached per file and only recalculated if the file has changed (by write time and length),
/// so a call after a file has been edited always sees the current content.
/// </summary>
internal sealed class NullForgivingScanner(SourceLocator locator, IAnalyzerEngine analyzerEngine, ILogger<NullForgivingScanner> logger)
{
    private const int MaxFilesPerWorkspace = 500;
    private const int MaxSourceLineLength = 240;

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    private sealed record CacheEntry(long Length, DateTime LastWriteTimeUtc, string? ProjectPath, IReadOnlyList<Occurrence> Occurrences);

    public async Task<ScanResult> ScanAsync(ScopeFilter scope, CancellationToken cancellationToken)
    {
        var inventory = await locator.GetInventoryAsync(scope, cancellationToken);

        // Tool calls can run in parallel; the cache is only filled by one scan at a time, so the work is not done twice.
        await _scanLock.WaitAsync(cancellationToken);

        try
        {
            var stale = inventory.Files.Where(file => !IsCurrent(file)).ToArray();
            var filesWithErrors = 0;

            foreach (var batch in stale.GroupBy(file => file.ProjectPath ?? string.Empty, PathComparer))
            {
                foreach (var chunk in batch.Chunk(MaxFilesPerWorkspace))
                {
                    filesWithErrors += await ScanChunkAsync(chunk, cancellationToken);
                }
            }

            var occurrences = inventory.Files
                .SelectMany(file => _cache.TryGetValue(file.FullPath, out var entry) ? entry.Occurrences : [])
                .ToArray();

            var info = new ScanInfo(
                inventory.Files.Count,
                stale.Length,
                inventory.Files.Count - stale.Length,
                inventory.FilesSkippedAsGenerated,
                inventory.FilesOutsideSolution,
                filesWithErrors);

            return new ScanResult(inventory.Files, occurrences, info);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private static readonly IEqualityComparer<string?> PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private bool IsCurrent(SourceFile file)
    {
        return _cache.TryGetValue(file.FullPath, out var entry)
               && entry.Length == file.Length
               && entry.LastWriteTimeUtc == file.LastWriteTimeUtc
               && PathComparer.Equals(entry.ProjectPath, file.ProjectPath);
    }

    /// <summary>
    /// Analyzes files of the same project, and returns the number of files that could not be read.
    /// </summary>
    private async Task<int> ScanChunkAsync(IReadOnlyList<SourceFile> files, CancellationToken cancellationToken)
    {
        var errors = 0;
        var filesByPath = new Dictionary<string, SourceFile>(PathComparer);

        using var workspace = new AdhocWorkspace();

        var projectName = files[0].ProjectName;
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None);

        var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, projectName, projectName, LanguageNames.CSharp, files[0].ProjectPath, parseOptions: parseOptions));

        foreach (var file in files)
        {
            try
            {
                SourceText text;

                await using (var stream = File.OpenRead(file.FullPath))
                {
                    text = SourceText.From(stream, Encoding.UTF8, throwIfBinaryDetected: false);
                }

                project = project.AddDocument(Path.GetFileName(file.FullPath), text, filePath: file.FullPath).Project;
                filesByPath[file.FullPath] = file;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Cannot read '{File}': {Message}", file.FullPath, ex.Message);
                errors++;
            }
        }

        var session = analyzerEngine.CreateSession(project.Documents, false, cancellationToken);

        await session.StartAsync();

        var occurrencesByFile = session.Results
            .OfType<NullForgivingAnalysisResult>()
            .GroupBy(result => result.FilePath, PathComparer)
            .ToDictionary(group => group.Key, group => group.ToArray(), PathComparer);

        foreach (var (path, file) in filesByPath)
        {
            var occurrences = occurrencesByFile.TryGetValue(path, out var results)
                ? results.OrderBy(result => result.Line).ThenBy(result => result.Column).Select(result => CreateOccurrence(file, result)).ToArray()
                : [];

            _cache[path] = new CacheEntry(file.Length, file.LastWriteTimeUtc, file.ProjectPath, occurrences);
        }

        return errors;
    }

    private static Occurrence CreateOccurrence(SourceFile file, NullForgivingAnalysisResult result)
    {
        var line = result.Line;
        var column = result.Column;
        var sourceLine = result.Variants[0].Node.SyntaxTree.GetText().Lines[line - 1].ToString().Trim();

        if (sourceLine.Length > MaxSourceLineLength)
        {
            sourceLine = sourceLine.Substring(0, MaxSourceLineLength) + "…";
        }

        return new Occurrence(
            $"{file.RelativePath}:{line}:{column}",
            file.ProjectName,
            file.RelativePath,
            line,
            column,
            result.Context,
            GetStatus(result.Justification),
            result.Justification,
            sourceLine);
    }

    private static string GetStatus(string? justification)
    {
        if (string.IsNullOrWhiteSpace(justification))
            return Status.Unjustified;

        return Justification.IsPlaceholder(justification) ? Status.Placeholder : Status.Justified;
    }
}
