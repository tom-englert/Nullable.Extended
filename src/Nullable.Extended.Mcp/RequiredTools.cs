using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Nullable.Extended.Mcp;

[McpServerToolType]
internal sealed class RequiredTools(NullForgivingScanner scanner, RequiredChecker checker, WorkspaceProvider workspaceProvider, ILogger<RequiredTools> logger)
{
    [McpServerTool(Name = "check_required", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Checks whether null-forgiving operators are still required: for each operator the compiler is asked if removing it produces a new nullable warning. " +
                 "This loads the projects with MSBuild and compiles them, which is slow and heavy compared to the other tools, so use it deliberately on a small scope: a few files, or a single project or folder; never speculatively. " +
                 "Every file needs a semantic analysis, and a call is limited to a small number of files (see the error message). " +
                 "The result per operator is 'yes' (required), 'no' (not required, a candidate for removal), 'unknown' (e.g. compilation errors, see the reason) or 'pending'. " +
                 "If the result is not complete within waitSeconds, the remaining operators are 'pending'; call again with the same arguments. " +
                 "The results stay valid until a source file of the project changes; list_null_forgiving shows them in 'required' and can filter by it. " +
                 "The projects should be built before, so references are available; the call loads the projects, which can write generated files into their 'obj' folders, but never changes source files. " +
                 "A 'no' result is a strong hint, not a proof: verify a removal with a build.")]
    public async Task<CheckResult> CheckRequired(
        McpServer server,
        [Description("Source files, relative to the root or absolute. Use either files or project.")] IReadOnlyList<string>? files = null,
        [Description("Check all files with null-forgiving operators of this project (name or project file path). Use either files or project.")] string? project = null,
        [Description("Only with project: restrict to files below this folder, relative to the root.")] string? path = null,
        [Description("How long to wait for the results, in seconds (default 20, maximum 60).")] int waitSeconds = RequiredChecker.DefaultWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("check_required: files={Files}, project={Project}, path={Path}, waitSeconds={WaitSeconds}", files?.Count, project, path, waitSeconds);

        var hasFiles = files is { Count: > 0 };
        var hasProject = !string.IsNullOrWhiteSpace(project);

        if (hasFiles == hasProject)
            throw new McpException("Specify either 'files' or 'project'.");

        if (!hasProject && !string.IsNullOrWhiteSpace(path))
            throw new McpException("'path' can only be used together with 'project'.");

        var locator = await workspaceProvider.GetLocatorAsync(server, cancellationToken);

        List<SourceFile> sourceFiles;
        var rejected = new List<FileProblem>();

        if (hasFiles)
        {
            sourceFiles = [];

            foreach (var resolution in await locator.ResolveFilesAsync(files!, cancellationToken))
            {
                if (resolution.File != null)
                    sourceFiles.Add(resolution.File);
                else
                    rejected.Add(new FileProblem(resolution.Input, "rejected", resolution.Problem ?? "The file can't be checked."));
            }
        }
        else
        {
            var scan = await scanner.ScanAsync(locator, new ScopeFilter(project, path), cancellationToken);
            var withOperators = scan.Occurrences.Select(occurrence => occurrence.File).ToHashSet(StringComparer.Ordinal);

            sourceFiles = scan.Files.Where(file => file.ProjectPath != null && withOperators.Contains(file.RelativePath)).ToList();
        }

        var result = await checker.CheckAsync(sourceFiles, waitSeconds, cancellationToken);

        return rejected.Count == 0 ? result : result with { Files = [.. rejected, .. result.Files] };
    }

    [McpServerTool(Name = "reload", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Discards everything the server has cached: the scan results and the loaded projects with their check_required results. " +
                 "Use it after files were added or removed, or if results look stale, e.g. after a branch switch.")]
    public async Task<string> Reload(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("reload");

        scanner.Reset();
        await checker.ResetAsync();

        return "Reloaded; the next calls scan and load again.";
    }
}
