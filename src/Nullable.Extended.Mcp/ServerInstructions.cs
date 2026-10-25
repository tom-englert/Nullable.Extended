namespace Nullable.Extended.Mcp;

/// <summary>
/// The short workflow that is sent to the client when it connects, for clients that don't support skills.
/// </summary>
internal static class ServerInstructions
{
    public const string Text =
        "Finds the null-forgiving operators ('!') of a C# code base by scanning the sources; no build is needed, and nothing is changed. " +
        "Start with get_summary, then work project by project: list_null_forgiving(project, status=unjustified) and get_code_context. " +
        "Treat every unjustified operator as a code problem first: fix the nullability annotations at the source, improve the null handling " +
        "(null checks, ArgumentNullException.ThrowIfNull, pattern matching, ?? and ?.), or restructure the code. " +
        "Only if none of this is practical add a justification comment above the statement or declaration, starting with '// ! ', " +
        "with a specific reason why the value can't be null; never generic text and never the 'TODO:' placeholder. " +
        "The scan is syntactic and does not tell whether an operator is still required: don't remove one unless a build of all target frameworks proves it. " +
        "Operator ids (file:line:column) change when a file is edited, so list again after every change.";
}
