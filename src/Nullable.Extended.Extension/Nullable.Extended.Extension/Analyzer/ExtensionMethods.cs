using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Extended.Extension.Analyzer
{
    public static class ExtensionMethods
    {
        public static string ReplaceNullForgivingToken(this string value)
        {
            var index = value.LastIndexOf('!');
            return new StringBuilder(value) { [index] = ' ' }.ToString();
        }

        public static IEnumerable<SyntaxNode> SelfAndDescendants(this SyntaxNode? node)
        {
            while (node != null)
            {
                yield return node;

                node = node.Parent;
            }
        }

        public static bool IsValid(this NullForgivingContext context)
        {
            return context == NullForgivingContext.General
                   || context == NullForgivingContext.Lambda
                   || context == NullForgivingContext.NullOrDefault;
        }

        public static NullForgivingContext GetContext(this PostfixUnaryExpressionSyntax node)
        {
            if (IsNullOrDefaultExpression(node))
            {
                return NullForgivingContext.NullOrDefault;
            }

            if (IsInsideLambdaExpression(node))
            {
                return NullForgivingContext.Lambda;
            }

            return NullForgivingContext.General;
        }

        private static bool IsInsideLambdaExpression(SyntaxNode node)
        {
            return (node.SelfAndDescendants().Any(n => n is LambdaExpressionSyntax));
        }

        private static bool IsNullOrDefaultExpression(SyntaxNode node)
        {
            if (!(node is PostfixUnaryExpressionSyntax e))
                return false;

            switch (e.Operand)
            {
                case LiteralExpressionSyntax l:
                    switch (l.Kind())
                    {
                        case SyntaxKind.DefaultLiteralExpression:
                        case SyntaxKind.NullLiteralExpression:
                            return true;
                    }

                    break;

                case DefaultExpressionSyntax _:
                    return true;
            }

            return false;
        }

    }
}