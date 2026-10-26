using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Nullable.Extended.Mcp;

/// <summary>
/// Determines the folder to work on for a request, in this order:
/// the <c>--root</c> argument, the workspace folder the client reports (MCP roots), the working directory of the server.
/// So the server normally needs no arguments; the client tells it what is open.
/// </summary>
internal sealed class WorkspaceProvider(ServerOptions options, ILogger<WorkspaceProvider> logger)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly ConcurrentDictionary<string, SourceLocator> _locators = new(PathComparer);

    public async Task<SourceLocator> GetLocatorAsync(McpServer server, CancellationToken cancellationToken)
    {
        string root;
        string source;

        if (options.Root != null)
        {
            root = options.Root;
            source = "--root argument";
        }
        else if (await GetClientRootAsync(server, cancellationToken) is { } clientRoot)
        {
            root = clientRoot;
            source = "client roots";
        }
        else
        {
            root = Directory.GetCurrentDirectory();
            source = "working directory";
        }

        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        logger.LogInformation("Using root '{Root}' ({Source})", root, source);

        if (!Directory.Exists(root))
            throw new McpException($"The root folder '{root}' does not exist.");

        return _locators.GetOrAdd(root, CreateLocator);
    }

    private SourceLocator CreateLocator(string root)
    {
        string? solutionPath = null;

        if (options.Solution != null)
        {
            solutionPath = Path.GetFullPath(options.Solution, root);

            if (!File.Exists(solutionPath))
                throw new McpException($"The solution file '{solutionPath}' does not exist.");
        }

        logger.LogInformation("Scanning below '{Root}'{Solution}", root, solutionPath != null ? $", restricted to the projects of '{solutionPath}'" : string.Empty);

        return new SourceLocator(root, solutionPath);
    }

    /// <summary>
    /// The workspace folder of the client; if it reports several, the one that contains the working directory of the server, else the first.
    /// </summary>
    private async Task<string?> GetClientRootAsync(McpServer server, CancellationToken cancellationToken)
    {
        // Roots are deprecated in the latest MCP spec revision, but still the only way to learn the open folder from current clients.
#pragma warning disable MCP9005
        if (server.ClientCapabilities?.Roots == null)
        {
            logger.LogInformation("The client does not support roots");

            return null;
        }

        try
        {
            var result = await server.RequestRootsAsync(new ListRootsRequestParams(), cancellationToken);
#pragma warning restore MCP9005

            var roots = result.Roots.Select(root => root.Uri).ToArray();

            logger.LogInformation("The client reports {Count} root(s): {Roots}", roots.Length, string.Join(", ", roots));

            return SelectRoot(roots, Directory.GetCurrentDirectory());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to get the roots from the client, using the working directory.");

            return null;
        }
    }

    /// <summary>
    /// Selects the local folder from the root URIs: the one that contains the working directory, else the first. Roots that are not local folders are ignored.
    /// </summary>
    internal static string? SelectRoot(IEnumerable<string> rootUris, string workingDirectory)
    {
        var roots = rootUris
            .Select(ToLocalPath)
            .OfType<string>()
            .ToArray();

        return roots.FirstOrDefault(root => workingDirectory.StartsWith(root, PathComparison)) ?? roots.FirstOrDefault();
    }

    /// <summary>
    /// Converts a <c>file:</c> URI to a local path. Uri.LocalPath can't be used, clients like VS Code percent-encode the colon of the drive, e.g. <c>file:///d%3A/dev/repo</c>.
    /// </summary>
    internal static string? ToLocalPath(string uriString)
    {
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri) || !uri.IsFile)
            return null;

        var path = Uri.UnescapeDataString(uri.AbsolutePath);

        if (OperatingSystem.IsWindows())
        {
            if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
            {
                path = path.Substring(1);
            }
            else if (!string.IsNullOrEmpty(uri.Host))
            {
                path = "//" + uri.Host + path;
            }

            path = path.Replace('/', Path.DirectorySeparatorChar);
        }

        return Path.TrimEndingDirectorySeparator(path);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
