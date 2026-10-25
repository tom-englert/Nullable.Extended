using System.ComponentModel;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using Nullable.Extended.NullForgivingAnalyzer;

namespace Nullable.Extended.Mcp;

internal sealed record StatusCounts(int Total, int Unjustified, int Placeholder, int Justified);

internal sealed record FolderSummary(string Path, StatusCounts Counts);

internal sealed record ProjectSummary(string Name, string? ProjectFile, StatusCounts Counts, IReadOnlyList<FolderSummary> Folders);

internal sealed record SummaryResult(
    string Root,
    ScanInfo Scan,
    StatusCounts Counts,
    IReadOnlyDictionary<string, int> ByContext,
    int ProjectsWithOccurrences,
    IReadOnlyList<ProjectSummary> Projects);

internal sealed record OccurrenceItem(string Id, string Project, string File, int Line, int Column, string Context, string DiagnosticId, string Status, string? Justification, string SourceLine);

internal sealed record ListResult(int Total, int Offset, int Returned, int? NextOffset, ScanInfo Scan, IReadOnlyList<OccurrenceItem> Items);

[McpServerToolType]
internal sealed class NullForgivingTools(NullForgivingScanner scanner, CodeContextReader codeContextReader, SourceLocator locator)
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;

    [McpServerTool(Name = "get_summary", ReadOnly = true)]
    [Description("Counts the null-forgiving operators (the '!' suffix) by project and folder, by status (unjustified: no justification comment, placeholder: only the 'TODO:' placeholder inserted by the code fix, justified) and by context (NX0001 general, NX0002 null/default, NX0003 lambda, NX0004 init-only property). " +
                 "Start here to plan the work project by project. This is a purely syntactic scan, it does not check whether an operator is required. " +
                 "Only the files that match the scope are scanned; without a scope, everything below the server's root is.")]
    public async Task<SummaryResult> GetSummary(
        [Description("Only files below this folder, or this file, relative to the root; or a glob pattern like 'src/**/Services/*.cs'.")] string? path = null,
        [Description("Only the files of this project: the project name, or the path of the project file relative to the root.")] string? project = null,
        [Description("Only files changed relative to this git ref (e.g. 'main' or 'origin/main'), including uncommitted and untracked files.")] string? changedSince = null,
        CancellationToken cancellationToken = default)
    {
        var scan = await scanner.ScanAsync(new ScopeFilter(project, path, changedSince), cancellationToken);
        var filesByPath = scan.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        var projects = scan.Occurrences
            .GroupBy(occurrence => filesByPath[occurrence.File].ProjectRelativePath ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = filesByPath[group.First().File];
                var projectDirectory = GetDirectory(first.ProjectRelativePath);

                var folders = group
                    .GroupBy(occurrence => GetFolder(occurrence.File, projectDirectory), StringComparer.Ordinal)
                    .Select(folder => new FolderSummary(folder.Key, Count(folder)))
                    .OrderByDescending(folder => folder.Counts.Unjustified)
                    .ThenBy(folder => folder.Path, StringComparer.Ordinal)
                    .ToArray();

                return new ProjectSummary(first.ProjectName, first.ProjectRelativePath, Count(group), folders);
            })
            .OrderByDescending(summary => summary.Counts.Unjustified)
            .ThenBy(summary => summary.Name, StringComparer.Ordinal)
            .ToArray();

        var byContext = scan.Occurrences
            .GroupBy(occurrence => occurrence.Context)
            .OrderBy(group => group.Key)
            .ToDictionary(group => $"{ContextNames.GetDiagnosticId(group.Key)} {group.Key}", group => group.Count());

        return new SummaryResult(locator.Root, scan.Info, Count(scan.Occurrences), byContext, projects.Length, projects);
    }

    [McpServerTool(Name = "list_null_forgiving", ReadOnly = true)]
    [Description("Lists null-forgiving operators with their location, context, status, justification and source line, sorted by file and position. " +
                 "The result is paged; narrow it down with the scope and filters instead of paging through everything, and use get_summary to plan. " +
                 "The id (file:line:column) changes when the file is edited, so list again after changing code. This is a purely syntactic scan, it does not check whether an operator is required.")]
    public async Task<ListResult> ListNullForgiving(
        [Description("Only the files of this project: the project name, or the path of the project file relative to the root.")] string? project = null,
        [Description("Only files below this folder, or this file, relative to the root; or a glob pattern like 'src/**/Services/*.cs'.")] string? path = null,
        [Description("Only files changed relative to this git ref (e.g. 'main' or 'origin/main'), including uncommitted and untracked files.")] string? changedSince = null,
        [Description("Only this context: general (NX0001), nullOrDefault (NX0002), lambda (NX0003) or init (NX0004).")] string? context = null,
        [Description("Only this status: unjustified, placeholder or justified.")] string? status = null,
        [Description("The maximum number of items to return (default 100, maximum 500).")] int limit = DefaultLimit,
        [Description("The number of items to skip, for paging; use 'nextOffset' of the previous result.")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        NullForgivingContext? contextFilter = null;

        if (!string.IsNullOrWhiteSpace(context))
        {
            if (!ContextNames.TryParse(context, out var parsedContext))
                throw new McpException($"'{context}' is not a valid context; use general, nullOrDefault, lambda, init or the diagnostic id.");

            contextFilter = parsedContext;
        }

        if (!string.IsNullOrWhiteSpace(status) && !Status.All.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new McpException($"'{status}' is not a valid status; use {string.Join(", ", Status.All)}.");

        limit = Math.Clamp(limit, 1, MaxLimit);
        offset = Math.Max(0, offset);

        var scan = await scanner.ScanAsync(new ScopeFilter(project, path, changedSince), cancellationToken);

        var matches = scan.Occurrences
            .Where(occurrence => contextFilter == null || occurrence.Context == contextFilter)
            .Where(occurrence => string.IsNullOrWhiteSpace(status) || string.Equals(occurrence.Status, status, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var items = matches
            .Skip(offset)
            .Take(limit)
            .Select(occurrence => new OccurrenceItem(
                occurrence.Id,
                occurrence.Project,
                occurrence.File,
                occurrence.Line,
                occurrence.Column,
                occurrence.Context.ToString(),
                ContextNames.GetDiagnosticId(occurrence.Context),
                occurrence.Status,
                occurrence.Justification,
                occurrence.SourceLine))
            .ToArray();

        var next = offset + items.Length;

        return new ListResult(matches.Length, offset, items.Length, next < matches.Length ? next : null, scan.Info, items);
    }

    [McpServerTool(Name = "get_code_context", ReadOnly = true)]
    [Description("Returns the source lines around a position, and for every null-forgiving operator in these lines its status and justification, " +
                 "and where a justification comment would have to be inserted (the line above which, and its indentation). " +
                 "Use it to understand why an operator is there, before fixing the code or writing a justification. Only files below the root can be read.")]
    public Task<CodeContext> GetCodeContext(
        [Description("The file, relative to the root (as returned by list_null_forgiving).")] string file,
        [Description("The 1-based line number.")] int line,
        [Description("The number of lines to return before and after the line (default 10, maximum 100).")] int radius = 10,
        CancellationToken cancellationToken = default)
    {
        return codeContextReader.ReadAsync(file, line, radius, cancellationToken);
    }

    private static StatusCounts Count(IEnumerable<Occurrence> occurrences)
    {
        var total = 0;
        var unjustified = 0;
        var placeholder = 0;
        var justified = 0;

        foreach (var occurrence in occurrences)
        {
            total++;

            switch (occurrence.Status)
            {
                case Status.Unjustified:
                    unjustified++;
                    break;
                case Status.Placeholder:
                    placeholder++;
                    break;
                default:
                    justified++;
                    break;
            }
        }

        return new StatusCounts(total, unjustified, placeholder, justified);
    }

    private static string GetDirectory(string? relativePath)
    {
        var index = relativePath?.LastIndexOf('/') ?? -1;

        return index < 0 ? string.Empty : relativePath!.Substring(0, index);
    }

    /// <summary>
    /// The first sub folder of the project folder the file is in, or the project folder itself.
    /// </summary>
    private static string GetFolder(string file, string projectDirectory)
    {
        var rest = projectDirectory.Length == 0 ? file : file.Substring(Math.Min(file.Length, projectDirectory.Length + 1));
        var index = rest.IndexOf('/');

        if (index < 0)
            return projectDirectory.Length == 0 ? "." : projectDirectory;

        var folder = rest.Substring(0, index);

        return projectDirectory.Length == 0 ? folder : projectDirectory + "/" + folder;
    }
}
