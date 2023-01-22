using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Shared
{
    internal static class Justification
    {
        public const string SuppressionCommentPrefix = "// ! ";

        private static readonly HashSet<SyntaxKind> SuppressionCommentTargetKind =
            new(Enum.GetValues(typeof(SyntaxKind)).Cast<SyntaxKind>().Where(IsSuppressionCommentTargetCandidate));

        public static bool HasJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .FindSuppressionCommentTarget()
                ?.GetReversJustificationLines(node)
                ?.Any()
                ?? false;
        }

        public static string? GetJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .FindSuppressionCommentTarget()
                ?.GetJustificationText(node);
        }

        private static IReadOnlyCollection<string>? GetReversJustificationLines(this SyntaxNode ancestor, PostfixUnaryExpressionSyntax node)
        {
            var lines = ancestor.DescendantNodesAndTokensAndSelf()
                .TakeWhile(item => item != node)
                .Where(item => item.Parent == ancestor)
                .Select(GetReversJustificationLines)
                .FirstOrDefault(item => item?.Count > 0);

            return lines;
        }

        private static string? GetJustificationText(this SyntaxNode ancestor, PostfixUnaryExpressionSyntax node)
        {
            var lines = ancestor.GetJustificationLines(node);
            return lines == null ? null : string.Join("\r\n", lines);
        }

        private static IEnumerable<string>? GetJustificationLines(this SyntaxNode ancestor, PostfixUnaryExpressionSyntax node)
        {
            return ancestor
                .GetReversJustificationLines(node)
                ?.Reverse();
        }

        private static IReadOnlyCollection<string>? GetReversJustificationLines(this SyntaxNodeOrToken node)
        {
            if (!node.HasLeadingTrivia)
                return null;

            var lines = node.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
                .Select(t => t.ToString())
                .Reverse()
                .TakeWhile(value => value.StartsWith(SuppressionCommentPrefix))
                .Select(s => s.Substring(SuppressionCommentPrefix.Length))
                .ToList()
                .AsReadOnly();

            return !lines.Any() || lines.All(string.IsNullOrWhiteSpace) ? null : lines;
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
