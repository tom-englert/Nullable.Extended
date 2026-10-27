namespace Nullable.Extended.Mcp;

/// <summary>
/// Whether a null-forgiving operator is required, i.e. whether removing it would cause a nullable warning.
/// </summary>
internal static class RequiredStates
{
    /// <summary>The operator has not been checked, or the result is no longer valid because the code has changed.</summary>
    public const string NotChecked = "notChecked";

    /// <summary>The check is still running; ask again.</summary>
    public const string Pending = "pending";

    /// <summary>Removing the operator would cause a nullable warning in at least one target framework.</summary>
    public const string Yes = "yes";

    /// <summary>Removing the operator causes no nullable warning in any target framework.</summary>
    public const string No = "no";

    /// <summary>It could not be determined, e.g. because of compile errors.</summary>
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [NotChecked, Pending, Yes, No, Unknown];
}

internal sealed record RequiredCounts(int Yes, int No, int Unknown, int Pending, int NotChecked);

internal sealed record RequiredItem(string Id, string File, int Line, int Column, string Status, string? Justification, string Required, string? Reason);

/// <summary>
/// The state of a project that has been loaded for a required check.
/// </summary>
/// <param name="State">ready, loading or failed.</param>
internal sealed record ProjectCheck(string Project, string? ProjectFile, string State, IReadOnlyList<string> TargetFrameworks, IReadOnlyList<string> Messages, bool BuildRecommended);

/// <summary>
/// A file that did not deliver results.
/// </summary>
/// <param name="State">pending (ask again) or rejected.</param>
internal sealed record FileProblem(string File, string State, string Message);

internal sealed record CheckResult(
    bool Complete,
    string? Hint,
    RequiredCounts Counts,
    int TotalItems,
    IReadOnlyList<ProjectCheck> Projects,
    IReadOnlyList<FileProblem> Files,
    IReadOnlyList<RequiredItem> Items);
