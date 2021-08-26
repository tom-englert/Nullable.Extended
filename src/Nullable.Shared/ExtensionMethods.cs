﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Shared
{
    internal static class ExtensionMethods
    {
        private const string SuppressionCommentPrefix = "// ! ";

        public static bool HasJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            var ancestor = node.FindAncestorStatementOrDeclaration();
            return ancestor?.GetRawJustificationLinesFromComments().Any() ?? false;
        }

        public static string? GetJustificationText(this PostfixUnaryExpressionSyntax node)
        {
            var ancestor = node.FindAncestorStatementOrDeclaration();
            var lines = ancestor.GetJustificationLinesFromComments();

            return lines.GetJustificationText();
        }


        public static string? GetJustificationText(this IEnumerable<string>? lines)
        {
            return lines == null ? null : string.Join(Environment.NewLine, lines);
        }

        public static IEnumerable<string>? GetJustificationLinesFromComments(this CSharpSyntaxNode? ancestor)
        {
            if (ancestor?.HasLeadingTrivia != true)
                return null;

            var rawLines = GetRawJustificationLinesFromComments(ancestor);
            return rawLines
                .Select(s => s.Substring(SuppressionCommentPrefix.Length))
                .Reverse();
        }

        public static IEnumerable<string>? GetRawJustificationLinesFromComments(this CSharpSyntaxNode? ancestor)
        {
            return ancestor?.GetLeadingTrivia()
                .Where(t => t.Kind() == SyntaxKind.SingleLineCommentTrivia)
                .Select(t => t.ToString())
                .Reverse()
                .TakeWhile(s => s.StartsWith(SuppressionCommentPrefix));
        }

        public static CSharpSyntaxNode? FindAncestorStatementOrDeclaration(this PostfixUnaryExpressionSyntax node)
        {
            return node
                .AncestorsAndSelf()
                .OfType<CSharpSyntaxNode>()
                .FirstOrDefault(i => i is StatementSyntax || i.GetType().Name.EndsWith("DeclarationSyntax"));
        }

        public static Location GetDiagnosticLocation(this PostfixUnaryExpressionSyntax node)
        {
            return node.OperatorToken.GetLocation();
        }

        public static bool IsInsideLambdaExpression(this SyntaxNode node)
        {
            return node.AncestorsAndSelf().Any(n => n is LambdaExpressionSyntax);
        }

        public static bool IsNullOrDefaultExpression(this SyntaxNode node)
        {
            if (node is not PostfixUnaryExpressionSyntax e)
                return false;

            switch (e.Operand)
            {
                case LiteralExpressionSyntax l:
#pragma warning disable IDE0010
// ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                    switch (l.Kind())
                    {
                        case SyntaxKind.DefaultLiteralExpression:
                        case SyntaxKind.NullLiteralExpression:
                            return true;
                    }

                    break;

                case DefaultExpressionSyntax:
                    return true;
            }

            return false;
        }

    }
}