using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Shared
{
    internal static class Justification
    {
        private const string SuppressionCommentPrefix = "// ! ";

        private static readonly HashSet<SyntaxKind> StatementOrDeclarationKind =
            new(Enum.GetValues(typeof(SyntaxKind)).Cast<SyntaxKind>().Where(IsStatementOrDeclaration));

        public static bool HasJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .FindAncestorStatementOrDeclaration()
                ?.GetReversJustificationLines(node)
                ?.Any()
                ?? false;
        }

        public static string? GetJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .FindAncestorStatementOrDeclaration()
                ?.GetJustificationText(node);
        }

        private static IReadOnlyCollection<string>? GetReversJustificationLines(this CSharpSyntaxNode ancestor, PostfixUnaryExpressionSyntax node)
        {
            var lines = ancestor.DescendantNodesAndTokensAndSelf()
                .TakeWhile(item => item != node)
                .Where(item => item.Parent == ancestor)
                .Select(GetReversJustificationLines)
                .FirstOrDefault(item => item?.Count > 0);

            return lines;
        }

        private static string? GetJustificationText(this CSharpSyntaxNode ancestor, PostfixUnaryExpressionSyntax node)
        {
            var lines = ancestor.GetJustificationLines(node);
            return lines == null ? null : string.Join(Environment.NewLine, lines);
        }

        private static IEnumerable<string>? GetJustificationLines(this CSharpSyntaxNode ancestor, PostfixUnaryExpressionSyntax node)
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
                .Where(t => t.Kind() == SyntaxKind.SingleLineCommentTrivia)
                .Select(t => t.ToString())
                .Reverse()
                .TakeWhile(value => value.StartsWith(SuppressionCommentPrefix))
                .Select(s => s.Substring(SuppressionCommentPrefix.Length))
                .ToList()
                .AsReadOnly();

            return !lines.Any() || lines.All(string.IsNullOrWhiteSpace) ? null : lines;
        }

        private static CSharpSyntaxNode? FindAncestorStatementOrDeclaration(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .AncestorsAndSelf()
                .OfType<CSharpSyntaxNode>()
                .FirstOrDefault(i => StatementOrDeclarationKind.Contains(i.Kind()));
        }

        private static bool IsStatementOrDeclaration(SyntaxKind syntaxKind)
        {
            var name = syntaxKind.ToString();
            return name.EndsWith("Statement", StringComparison.Ordinal) || name.EndsWith("Declaration", StringComparison.Ordinal);
        }
    }
}
