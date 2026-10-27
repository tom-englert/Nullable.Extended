using System.ComponentModel;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;

using Nullable.Shared;

namespace Nullable.Extended.Mcp;

internal sealed record AnnotationItem(
    [property: Description("The file, relative to the root (as returned by list_null_forgiving).")] string File,
    [property: Description("The 1-based line of the null-forgiving operator '!' (as returned by list_null_forgiving).")] int Line,
    [property: Description("The 1-based column of the null-forgiving operator '!' (as returned by list_null_forgiving).")] int Column,
    [property: Description("The justification: the specific reason why the value can't be null, or why the warning is a false positive. Plain text without the comment marker; several lines become several comment lines. Not 'TODO:'.")] string Text,
    [property: Description("The 'sourceLine' as returned by list_null_forgiving. Recommended: if given, the item is rejected when the line has changed in the meantime.")] string? SourceLine = null);

internal static class AnnotationOutcomes
{
    public const string Inserted = "inserted";
    public const string Replaced = "replaced";
    public const string Rejected = "rejected";
}

/// <param name="Id">The position of the operator as passed in, as file:line:column.</param>
/// <param name="Outcome">inserted: a justification was added; replaced: a placeholder was replaced by the justification; rejected: nothing was changed.</param>
/// <param name="Message">The reason, if the item was rejected.</param>
/// <param name="Status">The status of the operator after the change.</param>
/// <param name="Justification">The justification the operator has after the change.</param>
internal sealed record AnnotationOutcome(string Id, string Outcome, string? Message = null, string? Status = null, string? Justification = null);

internal sealed record AnnotateResult(int Inserted, int Replaced, int Rejected, IReadOnlyList<string> ChangedFiles, IReadOnlyList<AnnotationOutcome> Items);

/// <summary>
/// Writes justification comments above null-forgiving operators. This is the only operation of the server that changes files, and it only ever adds
/// <c>// ! </c> comment lines, or replaces the placeholder of the code fix. The placement is done by the same code as the code fix of the analyzer.
/// Every item is validated against the current content of the file, and rejected instead of guessed if the file is not as expected.
/// </summary>
internal sealed class Annotator(ILogger<Annotator> logger)
{
    public const int MaxItems = 500;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest, DocumentationMode.None);

    // Tool calls can run in parallel, but a file must not be read and written by two of them at the same time.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private sealed record PendingItem(int Index, string Id, string FullPath, AnnotationItem Item, IReadOnlyList<string> Lines);

    private sealed record AcceptedItem(PendingItem Pending, PostfixUnaryExpressionSyntax Node, CSharpSyntaxNode Target, bool ReplacesPlaceholder);

    public async Task<AnnotateResult> AnnotateAsync(SourceLocator locator, IReadOnlyList<AnnotationItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            throw new McpException("There are no items to annotate.");

        if (items.Count > MaxItems)
            throw new McpException($"Too many items, the maximum is {MaxItems} per call.");

        var outcomes = new AnnotationOutcome?[items.Count];
        var pending = new List<PendingItem>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            if (item == null)
            {
                outcomes[index] = Rejected($"item {index}", "The item is empty.");
                continue;
            }

            var id = $"{item.File}:{item.Line}:{item.Column}";

            try
            {
                var fullPath = locator.ResolveFile(item.File);
                id = $"{locator.GetRelativePath(fullPath)}:{item.Line}:{item.Column}";

                pending.Add(new PendingItem(index, id, fullPath, item, NormalizeText(item.Text)));
            }
            catch (McpException ex)
            {
                outcomes[index] = Rejected(id, ex.Message);
            }
        }

        var changedFiles = new List<string>();

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var rejectedFiles = await locator.FindRejectedFilesAsync(pending.Select(item => item.FullPath), cancellationToken);

            foreach (var file in pending.GroupBy(item => item.FullPath, PathComparer))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileItems = file.ToArray();

                if (rejectedFiles.TryGetValue(file.Key, out var reason))
                {
                    Reject(fileItems, outcomes, $"The file {reason}.");
                    continue;
                }

                try
                {
                    var fileOutcomes = ProcessFile(file.Key, fileItems);

                    foreach (var (index, outcome) in fileOutcomes)
                    {
                        outcomes[index] = outcome;
                    }

                    if (fileOutcomes.Values.Any(outcome => outcome.Outcome != AnnotationOutcomes.Rejected))
                    {
                        changedFiles.Add(locator.GetRelativePath(file.Key));
                    }
                }
                catch (Exception ex) when (ex is McpException or IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning("Cannot annotate '{File}': {Message}", file.Key, ex.Message);

                    Reject(fileItems, outcomes, $"The file has not been changed: {ex.Message}");
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }

        var results = outcomes.Select(outcome => outcome!).ToArray();

        var inserted = results.Count(outcome => outcome.Outcome == AnnotationOutcomes.Inserted);
        var replaced = results.Count(outcome => outcome.Outcome == AnnotationOutcomes.Replaced);

        logger.LogInformation("Annotated {Inserted} and replaced {Replaced} placeholder(s) of {Count} item(s) in {Files} file(s)", inserted, replaced, results.Length, changedFiles.Count);

        return new AnnotateResult(inserted, replaced, results.Length - inserted - replaced, changedFiles, results);
    }

    /// <summary>
    /// Validates the text of a justification, and returns its comment lines.
    /// </summary>
    internal static IReadOnlyList<string> NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new McpException("The text is empty; a justification has to say why the value can't be null, or why the warning is a false positive.");

        // Everything that ends a line in C# would end the comment, and let the rest become code.
        if (text.Any(c => (char.IsControl(c) && c is not ('\t' or '\r' or '\n')) || c is '\u2028' or '\u2029'))
            throw new McpException("The text contains control characters.");

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Any(line => line.StartsWith("//", StringComparison.Ordinal)))
            throw new McpException("The text must not contain the comment marker, it is added by the tool.");

        if (Justification.IsPlaceholder(lines[0]))
            throw new McpException($"The text is a placeholder ('{Justification.PlaceholderText}'); give the specific reason why the value can't be null, or why the warning is a false positive.");

        return lines;
    }

    private static Dictionary<int, AnnotationOutcome> ProcessFile(string fullPath, IReadOnlyList<PendingItem> items)
    {
        var fileInfo = new FileInfo(fullPath);
        var length = fileInfo.Length;
        var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

        var bytes = File.ReadAllBytes(fullPath);
        var (content, encoding) = Decode(bytes);

        var text = SourceText.From(content);
        var root = CSharpSyntaxTree.ParseText(text, ParseOptions, fullPath).GetRoot();
        var operators = GetOperators(root);

        var outcomes = new Dictionary<int, AnnotationOutcome>();
        var accepted = new List<AcceptedItem>();
        var usedNodes = new HashSet<PostfixUnaryExpressionSyntax>();

        foreach (var item in items)
        {
            var error = TryAccept(item, text, operators, usedNodes, out var acceptedItem);

            if (error != null)
            {
                outcomes[item.Index] = Rejected(item.Id, error);
            }
            else
            {
                accepted.Add(acceptedItem!);
            }
        }

        if (accepted.Count == 0)
            return outcomes;

        // Operators in the same statement share one justification, so their texts are merged into one comment block.
        var linesByTarget = accepted
            .OrderBy(item => item.Node.SpanStart)
            .GroupBy(item => item.Target)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.SelectMany(item => item.Pending.Lines).ToArray());

        var newRoot = root.ReplaceNodes(linesByTarget.Keys, (original, rewritten) => rewritten.SetJustification(linesByTarget[original]));
        var newContent = newRoot.ToFullString();

        var newOperators = GetOperators(CSharpSyntaxTree.ParseText(newContent, ParseOptions, fullPath).GetRoot());

        if (newOperators.Count != operators.Count)
            throw new McpException("The verification of the result failed, the number of operators has changed.");

        foreach (var item in accepted)
        {
            // Only trivia is added, so the operators are still in the same order.
            var expected = string.Join("\r\n", linesByTarget[item.Target]);
            var actual = newOperators[operators.IndexOf(item.Node)].GetJustificationText();

            if (actual != expected)
                throw new McpException($"The verification of the result failed for {item.Pending.Id}.");

            outcomes[item.Pending.Index] = new AnnotationOutcome(
                item.Pending.Id,
                item.ReplacesPlaceholder ? AnnotationOutcomes.Replaced : AnnotationOutcomes.Inserted,
                Status: Status.Justified,
                Justification: actual);
        }

        var after = new FileInfo(fullPath);

        if (after.Length != length || after.LastWriteTimeUtc != lastWriteTimeUtc)
            throw new McpException("The file has been changed while it was processed.");

        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(newContent);
        var result = new byte[preamble.Length + body.Length];

        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);

        File.WriteAllBytes(fullPath, result);

        return outcomes;
    }

    private static string? TryAccept(PendingItem pending, SourceText text, IReadOnlyList<PostfixUnaryExpressionSyntax> operators, HashSet<PostfixUnaryExpressionSyntax> usedNodes, out AcceptedItem? accepted)
    {
        accepted = null;

        var item = pending.Item;

        if (item.Line < 1 || item.Line > text.Lines.Count)
            return $"The file has {text.Lines.Count} lines, line {item.Line} is out of range.";

        var line = text.Lines[item.Line - 1];

        if (item.Column < 1 || item.Column > line.Span.Length)
            return $"Line {item.Line} has {line.Span.Length} characters, column {item.Column} is out of range.";

        var position = line.Start + item.Column - 1;
        var node = operators.FirstOrDefault(candidate => candidate.OperatorToken.SpanStart == position);

        if (node == null)
            return "There is no null-forgiving operator at this position; the file has probably been changed, list the operators again.";

        var actualLine = NullForgivingScanner.FormatSourceLine(line.ToString());

        if (item.SourceLine != null && !string.Equals(actualLine, NullForgivingScanner.FormatSourceLine(item.SourceLine), StringComparison.Ordinal))
            return $"The line has been changed, it is now: {actualLine}";

        if (!usedNodes.Add(node))
            return "There is more than one item for this operator.";

        var target = node.FindSuppressionCommentTarget();

        if (target == null)
            return "There is no statement or declaration to put the justification above.";

        var existing = node.GetJustificationText();

        if (string.IsNullOrWhiteSpace(existing))
        {
            accepted = new AcceptedItem(pending, node, target, false);

            return null;
        }

        if (!Justification.IsPlaceholder(existing))
            return $"The operator is already justified: {existing.Replace("\r\n", " ")}";

        if (!target.HasLeadingJustification())
            return "The placeholder is not located directly above the statement, replace it manually.";

        accepted = new AcceptedItem(pending, node, target, true);

        return null;
    }

    private static List<PostfixUnaryExpressionSyntax> GetOperators(SyntaxNode root)
    {
        return root
            .DescendantNodes()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            .ToList();
    }

    /// <summary>
    /// Source files are expected to be UTF-8, with or without BOM. Anything else is not written, because the encoding could not be preserved.
    /// </summary>
    private static (string Content, Encoding Encoding) Decode(byte[] bytes)
    {
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(hasBom, throwOnInvalidBytes: true);

        try
        {
            if (bytes.Length >= 2 && ((bytes[0] == 0xFE && bytes[1] == 0xFF) || (bytes[0] == 0xFF && bytes[1] == 0xFE)))
                throw new McpException("The file is not UTF-8 encoded.");

            var offset = hasBom ? 3 : 0;

            return (encoding.GetString(bytes, offset, bytes.Length - offset), encoding);
        }
        catch (DecoderFallbackException)
        {
            throw new McpException("The file is not UTF-8 encoded.");
        }
    }

    private static void Reject(IEnumerable<PendingItem> items, AnnotationOutcome?[] outcomes, string message)
    {
        foreach (var item in items)
        {
            outcomes[item.Index] = Rejected(item.Id, message);
        }
    }

    private static AnnotationOutcome Rejected(string id, string message) => new(id, AnnotationOutcomes.Rejected, message);
}
