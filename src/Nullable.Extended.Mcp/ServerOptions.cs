using Microsoft.Extensions.Configuration;

namespace Nullable.Extended.Mcp;

/// <summary>
/// The command line options of the server. Both are optional; without a root the server uses the workspace folder of the client.
/// </summary>
/// <param name="Root">The folder below which the sources are scanned.</param>
/// <param name="Solution">Restricts the scan to the projects of this solution; relative to the root, or absolute.</param>
internal sealed record ServerOptions(string? Root, string? Solution)
{
    /// <summary>
    /// Reads <c>--root &lt;folder&gt;</c> and <c>--solution &lt;file&gt;</c>.
    /// </summary>
    public static ServerOptions Create(IConfiguration configuration)
    {
        string? root = null;

        if (configuration["root"] is { Length: > 0 } rootValue)
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootValue));

            if (!Directory.Exists(root))
                throw new ArgumentException($"The root folder '{root}' does not exist.");
        }

        return new ServerOptions(root, configuration["solution"] is { Length: > 0 } solution ? solution : null);
    }
}
