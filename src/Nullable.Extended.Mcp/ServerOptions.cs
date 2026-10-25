using Microsoft.Extensions.Configuration;

namespace Nullable.Extended.Mcp;

/// <summary>
/// The command line options of the server.
/// </summary>
/// <param name="Root">The folder below which the sources are scanned.</param>
/// <param name="SolutionPath">Optional, restricts the scan to the projects of this solution.</param>
internal sealed record ServerOptions(string Root, string? SolutionPath)
{
    /// <summary>
    /// Reads <c>--root &lt;folder&gt;</c> (default: the working directory) and <c>--solution &lt;file&gt;</c> (relative to the root, or absolute).
    /// </summary>
    public static ServerOptions Create(IConfiguration configuration)
    {
        var root = Path.GetFullPath(configuration["root"] is { Length: > 0 } rootValue ? rootValue : Directory.GetCurrentDirectory());

        if (!Directory.Exists(root))
            throw new ArgumentException($"The root folder '{root}' does not exist.");

        string? solutionPath = null;

        if (configuration["solution"] is { Length: > 0 } solutionValue)
        {
            solutionPath = Path.GetFullPath(solutionValue, root);

            if (!File.Exists(solutionPath))
                throw new ArgumentException($"The solution file '{solutionPath}' does not exist.");
        }

        return new ServerOptions(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), solutionPath);
    }
}
