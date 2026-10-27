using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Shared
{
    public static class Justification
    {
        public const string SuppressionCommentPrefix = "// ! ";

        /// <summary>
        /// The text the code fix inserts after the <see cref="SuppressionCommentPrefix"/>; it counts as a justification, but has to be replaced by a real one.
        /// </summary>
        public const string PlaceholderText = "TODO:";

        public static bool IsPlaceholder(string? justificationText)
        {
            return justificationText != null && justificationText.TrimStart().StartsWith(PlaceholderText, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly HashSet<SyntaxKind> SuppressionCommentTargetKind =
            [.. Enum.GetValues(typeof(SyntaxKind)).Cast<SyntaxKind>().Where(IsSuppressionCommentTargetCandidate)];

        public static bool HasJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            return node.GetJustificationLines() != null;
        }

        public static string? GetJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            var lines = node.GetJustificationLines();

            return lines == null ? null : string.Join("\r\n", lines);
        }

        /// <summary>
        /// The justification is the block of suppression comment lines directly above the first part of the target
        /// that has one. Only the parts that precede the node, or contain it, are considered.
        /// </summary>
        private static IReadOnlyList<string>? GetJustificationLines(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .FindSuppressionCommentTarget()
                ?.ChildNodesAndTokens()
                .TakeWhile(child => child.FullSpan.Start <= node.FullSpan.Start)
                .Select(GetLeadingJustificationLines)
                .FirstOrDefault(lines => lines != null);
        }

        private static IReadOnlyList<string>? GetLeadingJustificationLines(SyntaxNodeOrToken nodeOrToken)
        {
            var lines = nodeOrToken.GetLeadingTrivia()
                .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                .Select(trivia => trivia.ToString())
                .Reverse()
                .TakeWhile(comment => comment.StartsWith(SuppressionCommentPrefix))
                .Select(comment => comment.Substring(SuppressionCommentPrefix.Length))
                .Reverse()
                .ToList();

            return lines.All(string.IsNullOrWhiteSpace) ? null : lines;
        }

        public static CSharpSyntaxNode? FindSuppressionCommentTarget(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .AncestorsAndSelf()
                .OfType<CSharpSyntaxNode>()
                .FirstOrDefault(i => SuppressionCommentTargetKind.Contains(i.Kind()));
        }

        /// <summary>
        /// True if there is a justification directly above the target, i.e. in the leading trivia of the target itself.
        /// </summary>
        public static bool HasLeadingJustification(this CSharpSyntaxNode target)
        {
            return GetLeadingJustificationLines(target) != null;
        }

        /// <summary>
        /// Puts the justification above the target, one <see cref="SuppressionCommentPrefix"/> comment per line, with the indentation and line ending of the target.
        /// A suppression comment that is already directly above the target is replaced.
        /// </summary>
        public static TNode SetJustification<TNode>(this TNode target, IReadOnlyList<string> lines) where TNode : SyntaxNode
        {
            var originalTrivia = target.GetLeadingTrivia();
            var trivia = originalTrivia.ToList();
            var indent = RemoveTrailingWhitespace(trivia);

            // replace an existing justification
            while (trivia.Count >= 2 && trivia[trivia.Count - 1].IsKind(SyntaxKind.EndOfLineTrivia) && IsSuppressionComment(trivia[trivia.Count - 2]))
            {
                trivia.RemoveRange(trivia.Count - 2, 2);
                RemoveTrailingWhitespace(trivia);
            }

            // reuse the line ending of the document; only fall back to CRLF if the target has no line ending at all
            var newline = originalTrivia
                .Concat(target.GetTrailingTrivia())
                .FirstOrDefault(item => item.IsKind(SyntaxKind.EndOfLineTrivia));

            if (newline == default)
            {
                newline = SyntaxFactory.CarriageReturnLineFeed;
            }

            foreach (var line in lines)
            {
                if (indent != default)
                {
                    trivia.Add(indent);
                }

                trivia.Add(SyntaxFactory.Comment(SuppressionCommentPrefix + line));
                trivia.Add(newline);
            }

            if (indent != default)
            {
                trivia.Add(indent);
            }

            return target.WithLeadingTrivia(SyntaxFactory.TriviaList(trivia));
        }

        private static bool IsSuppressionComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().StartsWith(SuppressionCommentPrefix, StringComparison.Ordinal);
        }

        private static SyntaxTrivia RemoveTrailingWhitespace(List<SyntaxTrivia> trivia)
        {
            if (trivia.Count == 0 || !trivia[trivia.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                return default;

            var whitespace = trivia[trivia.Count - 1];
            trivia.RemoveAt(trivia.Count - 1);

            return whitespace;
        }

        private static bool IsSuppressionCommentTargetCandidate(SyntaxKind syntaxKind)
        {
            if (syntaxKind == SyntaxKind.VariableDeclaration)
                return false; // always use the parent declaration for variable declarations!

            var name = syntaxKind.ToString();
            return name.EndsWith("Statement", StringComparison.Ordinal) || name.EndsWith("Declaration", StringComparison.Ordinal);
        }
    }
}
