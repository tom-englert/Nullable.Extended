using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using ModelContextProtocol;

using Nullable.Shared;

namespace Nullable.Extended.Mcp;

internal sealed record SourceLineInfo(int Number, string Text);

/// <param name="Line">The line the comment has to be inserted above; the target is the closest enclosing statement or declaration of the operator.</param>
/// <param name="Indentation">The indentation of the inserted comment, i.e. the leading whitespace of the target line.</param>
/// <param name="TargetKind">The syntax kind of the statement or declaration the comment belongs to.</param>
/// <param name="TargetStartsLine">False if the target is preceded by other code in its line, e.g. an embedded statement after an <c>if (...)</c>.</param>
internal sealed record CommentInsertion(int Line, string Indentation, string TargetKind, bool TargetStartsLine);

internal sealed record OperatorInfo(string Id, int Line, int Column, string Context, string DiagnosticId, string Status, string? Justification, CommentInsertion? Insertion);

internal sealed record CodeContext(string File, int TotalLines, string LineEnding, int StartLine, int EndLine, IReadOnlyList<SourceLineInfo> Lines, IReadOnlyList<OperatorInfo> Operators);

/// <summary>
/// Reads the source around a position, so the user of the server can see what a null-forgiving operator is used for and where its justification belongs.
/// </summary>
internal sealed class CodeContextReader(SourceLocator locator, NullForgivingScanner scanner)
{
    private const int MaxRadius = 100;

    public async Task<CodeContext> ReadAsync(string file, int line, int radius, CancellationToken cancellationToken)
    {
        var fullPath = locator.ResolveFile(file);

        if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            throw new McpException($"'{file}' is not a C# source file.");

        if (!File.Exists(fullPath))
            throw new McpException($"The file '{file}' does not exist.");

        var relativePath = locator.GetRelativePath(fullPath);

        // The operators are classified by the engine, like in every other tool.
        var scan = await scanner.ScanAsync(new ScopeFilter(Path: relativePath), cancellationToken);

        SourceText text;

        await using (var stream = File.OpenRead(fullPath))
        {
            text = SourceText.From(stream, Encoding.UTF8, throwIfBinaryDetected: false);
        }

        var lines = text.Lines;

        if (line < 1 || line > lines.Count)
            throw new McpException($"The file '{file}' has {lines.Count} lines, line {line} is out of range.");

        radius = Math.Clamp(radius, 0, MaxRadius);

        var startLine = Math.Max(1, line - radius);
        var endLine = Math.Min(lines.Count, line + radius);

        var sourceLines = Enumerable.Range(startLine, endLine - startLine + 1)
            .Select(number => new SourceLineInfo(number, lines[number - 1].ToString()))
            .ToArray();

        var occurrences = scan.Occurrences
            .Where(occurrence => occurrence.Line >= startLine && occurrence.Line <= endLine)
            .ToArray();

        var operators = new List<OperatorInfo>();

        if (occurrences.Length <= 0)
            return new CodeContext(relativePath, lines.Count, DetectLineEnding(text), startLine, endLine, sourceLines, operators);

        var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None), fullPath, cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        foreach (var occurrence in occurrences)
        {
            var position = lines.GetPosition(new LinePosition(occurrence.Line - 1, occurrence.Column - 1));
            var node = root.FindToken(position).Parent as PostfixUnaryExpressionSyntax;

            operators.Add(new OperatorInfo(
                occurrence.Id,
                occurrence.Line,
                occurrence.Column,
                occurrence.Context.ToString(),
                ContextNames.GetDiagnosticId(occurrence.Context),
                occurrence.Status,
                occurrence.Justification,
                node != null ? CreateInsertion(node, text) : null));
        }

        return new CodeContext(relativePath, lines.Count, DetectLineEnding(text), startLine, endLine, sourceLines, operators);
    }

    private static CommentInsertion? CreateInsertion(PostfixUnaryExpressionSyntax node, SourceText text)
    {
        var target = node.FindSuppressionCommentTarget();

        if (target == null)
            return null;

        var firstToken = target.GetFirstToken();
        var targetLine = text.Lines[firstToken.GetLocation().GetLineSpan().StartLinePosition.Line];
        var indentation = new string(targetLine.ToString().TakeWhile(char.IsWhiteSpace).ToArray());

        return new CommentInsertion(targetLine.LineNumber + 1, indentation, target.Kind().ToString(), firstToken.SpanStart - targetLine.Start == indentation.Length);
    }

    private static string DetectLineEnding(SourceText text)
    {
        var lines = text.Lines;

        return lines.Count > 1 && lines[0].EndIncludingLineBreak - lines[0].End == 2 ? "\r\n" : "\n";
    }
}
