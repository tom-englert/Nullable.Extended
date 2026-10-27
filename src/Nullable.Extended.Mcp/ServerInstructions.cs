namespace Nullable.Extended.Mcp;

/// <summary>
/// The short workflow that is sent to the client when it connects, for clients that don't support skills.
/// </summary>
internal static class ServerInstructions
{
    public const string Text =
        "Finds the null-forgiving operators ('!') of a C# code base by scanning the sources; no build is needed, and only the annotate tool changes files. " +
        "Start with get_summary, then work project by project: list_null_forgiving(project, status=unjustified) and get_code_context. " +
        "Treat every unjustified operator as a code problem first: fix the nullability annotations at the source, improve the null handling " +
        "(null checks, ArgumentNullException.ThrowIfNull, pattern matching, ?? and ?.), or restructure the code. " +
        "Only if none of this is practical write a justification with the annotate tool (batches per project, pass the sourceLine of the list); " +
        "it adds the '// ! ' comment above the statement or declaration. The text must be a specific reason why the value can't be null; never generic text and never the 'TODO:' placeholder. " +
        "The scan is syntactic and does not tell whether an operator is still required. check_required does, by compiling the project: it is slow, so use it only for a few files or one project, and call it again if it is not complete. " +
        "Remove an operator only if check_required says 'no' and a build of all target frameworks proves it; never on 'unknown', 'pending' or 'notChecked'. " +
        "Operator ids (file:line:column) change when a file is edited, so list again after every change.";
}
