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
        var root = options.Root
                   ?? await GetClientRootAsync(server, cancellationToken)
                   ?? Directory.GetCurrentDirectory();

        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

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
            return null;

        try
        {
            var result = await server.RequestRootsAsync(new ListRootsRequestParams(), cancellationToken);
#pragma warning restore MCP9005

            var roots = result.Roots.Select(root => root.Uri).ToArray();

            if (roots.Length > 1)
                logger.LogInformation("The client reports several roots: {Roots}", string.Join(", ", roots));

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
            .Select(root => Uri.TryCreate(root, UriKind.Absolute, out var uri) && uri.IsFile ? Path.TrimEndingDirectorySeparator(uri.LocalPath) : null)
            .OfType<string>()
            .ToArray();

        return roots.FirstOrDefault(root => workingDirectory.StartsWith(root, PathComparison)) ?? roots.FirstOrDefault();
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
