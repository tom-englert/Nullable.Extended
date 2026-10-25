using System.Diagnostics;
using System.IO.Enumeration;

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

using ModelContextProtocol;

namespace Nullable.Extended.Mcp;

/// <summary>
/// Restricts the files a request works on. All criteria have to match.
/// </summary>
/// <param name="Project">A project name or the relative path of a project file.</param>
/// <param name="Path">A folder or file relative to the root, or a glob pattern like <c>src/**/Services/*.cs</c>.</param>
/// <param name="ChangedSince">A git ref; only files that have changed relative to it, including uncommitted and untracked files.</param>
internal sealed record ScopeFilter(string? Project = null, string? Path = null, string? ChangedSince = null);

internal sealed record SourceInventory(IReadOnlyList<SourceFile> Files, int FilesSkippedAsGenerated, int FilesOutsideSolution);

/// <summary>
/// Finds the source files below the root and the projects they belong to, without loading any project.
/// The project of a file is the nearest project file in the folder of the file or above; this is exact for SDK style projects with the default globbing.
/// </summary>
internal sealed class SourceLocator(ServerOptions options)
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string Root => options.Root;

    public async Task<SourceInventory> GetInventoryAsync(ScopeFilter scope, CancellationToken cancellationToken)
    {
        var solutionProjects = options.SolutionPath != null ? await ReadSolutionProjectsAsync(options.SolutionPath, cancellationToken) : null;
        var changedFiles = scope.ChangedSince != null ? await GetChangedFilesAsync(scope.ChangedSince, cancellationToken) : null;
        var pathMatcher = CreatePathMatcher(scope.Path);

        var projectCache = new Dictionary<string, string?>(PathComparer);
        var files = new List<SourceFile>();
        var skippedAsGenerated = 0;
        var outsideSolution = 0;

        foreach (var (fullPath, length, lastWriteTimeUtc) in EnumerateSourceFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = GetRelativePath(fullPath);

            if (changedFiles != null && !changedFiles.Contains(fullPath))
                continue;

            if (pathMatcher != null && !pathMatcher(relativePath))
                continue;

            var projectPath = FindProject(Path.GetDirectoryName(fullPath)!, projectCache);

            if (solutionProjects != null && (projectPath == null || !solutionProjects.Contains(projectPath)))
            {
                outsideSolution++;
                continue;
            }

            var projectName = projectPath != null ? Path.GetFileNameWithoutExtension(projectPath) : SourceFile.NoProjectName;
            var projectRelativePath = projectPath != null ? GetRelativePath(projectPath) : null;

            if (!string.IsNullOrEmpty(scope.Project)
                && !string.Equals(projectName, scope.Project, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(projectRelativePath, scope.Project.Replace('\\', '/'), PathComparison))
                continue;

            if (!AnalyzerFramework.ExtensionMethods.ShouldBeAnalyzed(fullPath, projectName))
            {
                skippedAsGenerated++;
                continue;
            }

            files.Add(new SourceFile(fullPath, relativePath, projectPath, projectName, projectRelativePath, length, lastWriteTimeUtc));
        }

        files.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));

        return new SourceInventory(files, skippedAsGenerated, outsideSolution);
    }

    /// <summary>
    /// Resolves a path that has been passed by a client to a file below the root; fails if it points elsewhere.
    /// </summary>
    public string ResolveFile(string file)
    {
        var fullPath = Path.GetFullPath(file, options.Root);

        if (!fullPath.StartsWith(options.Root + Path.DirectorySeparatorChar, PathComparison))
            throw new McpException($"The file '{file}' is not located below the root folder.");

        return fullPath;
    }

    public string GetRelativePath(string fullPath)
    {
        var relativePath = Path.GetRelativePath(options.Root, fullPath);

        return Path.DirectorySeparatorChar == '/' ? relativePath : relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private IEnumerable<(string FullPath, long Length, DateTime LastWriteTimeUtc)> EnumerateSourceFiles()
    {
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
        };

        return new FileSystemEnumerable<(string, long, DateTime)>(options.Root, Transform, enumerationOptions)
        {
            ShouldIncludePredicate = static (ref entry) => !entry.IsDirectory && entry.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase),
            ShouldRecursePredicate = static (ref entry) => !IsExcludedDirectory(entry.FileName)
        };

        static (string, long, DateTime) Transform(ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.UtcDateTime);
    }

    private static bool IsExcludedDirectory(ReadOnlySpan<char> name)
    {
        return name.StartsWith(".")
               || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
               || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
               || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindProject(string directory, Dictionary<string, string?> cache)
    {
        if (cache.TryGetValue(directory, out var cached))
            return cached;

        string? projectPath = null;

        try
        {
            projectPath = Directory
                .EnumerateFiles(directory, "*.csproj")
                .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // treat unreadable folders like folders without a project file
        }

        if (projectPath == null && Path.GetDirectoryName(directory) is { } parent)
        {
            projectPath = FindProject(parent, cache);
        }

        cache[directory] = projectPath;

        return projectPath;
    }

    private static Func<string, bool>? CreatePathMatcher(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Trim().Replace('\\', '/').TrimStart('/');

        if (path.Contains('*') || path.Contains('?'))
        {
            var matcher = new Matcher(PathComparison == StringComparison.Ordinal ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(path);

            return relativePath => matcher.Match(relativePath).HasMatches;
        }

        path = path.TrimEnd('/');

        // a folder or a single file
        return relativePath => string.Equals(relativePath, path, PathComparison)
                               || relativePath.StartsWith(path + "/", PathComparison);
    }

    private static async Task<HashSet<string>> ReadSolutionProjectsAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath)
                         ?? throw new McpException($"'{solutionPath}' is not a supported solution file.");

        var solution = await serializer.OpenAsync(solutionPath, cancellationToken);
        var solutionFolder = Path.GetDirectoryName(solutionPath)!;

        return solution.SolutionProjects
            .Select(project => Path.GetFullPath(project.FilePath.Replace('\\', Path.DirectorySeparatorChar), solutionFolder))
            .ToHashSet(PathComparer);
    }

    private async Task<HashSet<string>> GetChangedFilesAsync(string gitRef, CancellationToken cancellationToken)
    {
        if (gitRef.StartsWith('-'))
            throw new McpException($"'{gitRef}' is not a valid git ref.");

        if (await RunGitAsync(["rev-parse", "--is-inside-work-tree"], cancellationToken) is not "true")
            throw new McpException($"The root folder '{options.Root}' is not inside a git repository, 'changedSince' cannot be used.");

        // Paths are relative to the root folder, which is the working directory of git.
        var changed = await RunGitAsync(["diff", "--name-only", "--relative", "--diff-filter=ACMR", "-z", gitRef, "--"], cancellationToken);
        var untracked = await RunGitAsync(["ls-files", "--others", "--exclude-standard", "-z"], cancellationToken);

        return (changed + "\0" + untracked)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(path, options.Root))
            .ToHashSet(PathComparer);
    }

    private async Task<string> RunGitAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = options.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;

        try
        {
            process = Process.Start(startInfo) ?? throw new McpException("Failed to start git.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new McpException($"git is required for 'changedSince', but could not be started: {ex.Message}");
        }

        using (process)
        {
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new McpException($"git {string.Join(' ', arguments)} failed: {(await error).Trim()}");

            return (await output).Trim('\n', '\r', ' ');
        }
    }
}
