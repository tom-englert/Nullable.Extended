using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Shared;

namespace Nullable.Extended.Extension.NullForgivingAnalyzer
{
    public static class ExtensionMethods
    {
        public static string ReplaceNullForgivingToken(this string value)
        {
            var index = value.LastIndexOf('!');
            return new StringBuilder(value) { [index] = ' ' }.ToString();
        }

        public static NullForgivingContext GetContext(this PostfixUnaryExpressionSyntax node)
        {
            if (node.IsNullOrDefaultExpression(out var expression))
            {
                return expression.IsInitOnlyPropertyAssignment() ? NullForgivingContext.Init : NullForgivingContext.NullOrDefault;
            }

            if (node.IsInsideLambdaExpression())
            {
                return NullForgivingContext.Lambda;
            }

            return NullForgivingContext.General;
        }
    }
}
