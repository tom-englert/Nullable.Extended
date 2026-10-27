using Microsoft.Extensions.Configuration;

namespace Nullable.Extended.Mcp;

/// <summary>
/// The command line options of the server. Both are optional; without a root the server uses the workspace folder of the client.
/// </summary>
/// <param name="Root">The folder below which the sources are scanned.</param>
/// <param name="Solution">Restricts the scan to the projects of this solution; relative to the root, or absolute.</param>
/// <param name="MaxCheckFiles">The maximum number of files a single required check may cover.</param>
internal sealed record ServerOptions(string? Root, string? Solution, int MaxCheckFiles = ServerOptions.DefaultMaxCheckFiles)
{
    public const int DefaultMaxCheckFiles = 25;

    /// <summary>
    /// Reads <c>--root &lt;folder&gt;</c>, <c>--solution &lt;file&gt;</c> and <c>--max-check-files &lt;count&gt;</c>.
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

        var maxCheckFiles = DefaultMaxCheckFiles;

        if (configuration["max-check-files"] is { Length: > 0 } maxCheckFilesValue
            && (!int.TryParse(maxCheckFilesValue, out maxCheckFiles) || maxCheckFiles < 1))
            throw new ArgumentException($"'{maxCheckFilesValue}' is not a valid value for --max-check-files, use a positive number.");

        return new ServerOptions(root, configuration["solution"] is { Length: > 0 } solution ? solution : null, maxCheckFiles);
    }
}
