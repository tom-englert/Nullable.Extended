using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

namespace Nullable.Extended.Mcp;

[McpServerToolType]
internal sealed class AnnotationTools(Annotator annotator, WorkspaceProvider workspaceProvider, ILogger<AnnotationTools> logger)
{
    [McpServerTool(Name = "annotate", Destructive = false, OpenWorld = false)]
    [Description("Writes justification comments ('// ! reason') above null-forgiving operators, the last resort for operators that can't reasonably be fixed in the code. " +
                 "This is the only tool that changes files. Fixing the code is always preferred; use it only for operators that are really needed, and give the specific reason why the value can't be null or why the warning is a false positive. " +
                 "Never generic text like 'needed' or 'suppress warning', and never 'TODO:' (rejected). " +
                 "Pass the file, line and column from list_null_forgiving, the text, and its sourceLine; items are rejected if the operator isn't there any more or is already justified. A placeholder ('// ! TODO:') is replaced. " +
                 "Items are validated one by one, the others are still written. The comment is put above the statement or declaration of the operator, with its indentation and line ending, and operators in the same statement share one justification. " +
                 "Inserting comments moves the following lines of the file, so list the operators again before the next call.")]
    public async Task<AnnotateResult> Annotate(
        McpServer server,
        [Description("The justifications to write, at most 500 per call; prefer batches of one project or folder, so they can be reviewed in the diff.")] IReadOnlyList<AnnotationItem> items,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("annotate: {Count} item(s)", items.Count);

        var locator = await workspaceProvider.GetLocatorAsync(server, cancellationToken);

        return await annotator.AnnotateAsync(locator, items, cancellationToken);
    }
}
