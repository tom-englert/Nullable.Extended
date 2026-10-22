using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Shared
{
    internal static class Justification
    {
        public const string SuppressionCommentPrefix = "// ! ";

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

        private static bool IsSuppressionCommentTargetCandidate(SyntaxKind syntaxKind)
        {
            if (syntaxKind == SyntaxKind.VariableDeclaration)
                return false; // always use the parent declaration for variable declarations!

            var name = syntaxKind.ToString();
            return name.EndsWith("Statement", StringComparison.Ordinal) || name.EndsWith("Declaration", StringComparison.Ordinal);
        }
    }
}
