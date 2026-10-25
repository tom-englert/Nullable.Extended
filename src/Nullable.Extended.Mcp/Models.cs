using Nullable.Extended.NullForgivingAnalyzer;

namespace Nullable.Extended.Mcp;

internal static class Status
{
    public const string Unjustified = "unjustified";
    public const string Placeholder = "placeholder";
    public const string Justified = "justified";

    public static readonly IReadOnlyList<string> All = [Unjustified, Placeholder, Justified];
}

internal static class ContextNames
{
    public static string GetDiagnosticId(NullForgivingContext context) => context switch
    {
        NullForgivingContext.General => "NX0001",
        NullForgivingContext.NullOrDefault => "NX0002",
        NullForgivingContext.Lambda => "NX0003",
        NullForgivingContext.Init => "NX0004",
        _ => throw new ArgumentOutOfRangeException(nameof(context), context, null)
    };

    public static bool TryParse(string? value, out NullForgivingContext context)
    {
        context = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var candidate in Enum.GetValues<NullForgivingContext>())
        {
            if (string.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, GetDiagnosticId(candidate), StringComparison.OrdinalIgnoreCase))
            {
                context = candidate;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// A source file below the root, with the project it belongs to.
/// </summary>
internal sealed record SourceFile(string FullPath, string RelativePath, string? ProjectPath, string ProjectName, string? ProjectRelativePath, long Length, DateTime LastWriteTimeUtc)
{
    public const string NoProjectName = "(no project)";
}

/// <summary>
/// A null-forgiving operator as found by the syntax scan.
/// </summary>
internal sealed record Occurrence(
    string Id,
    string Project,
    string File,
    int Line,
    int Column,
    NullForgivingContext Context,
    string Status,
    string? Justification,
    string SourceLine);

internal sealed record ScanInfo(int FilesInScope, int FilesScanned, int FilesFromCache, int FilesSkippedAsGenerated, int FilesOutsideSolution, int FilesWithErrors);

internal sealed record ScanResult(IReadOnlyList<SourceFile> Files, IReadOnlyList<Occurrence> Occurrences, ScanInfo Info);
