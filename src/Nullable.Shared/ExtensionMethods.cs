using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullable.Shared
{
    internal static class ExtensionMethods
    {
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
