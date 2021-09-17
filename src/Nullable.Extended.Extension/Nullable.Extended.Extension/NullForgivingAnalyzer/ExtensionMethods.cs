using System.Text;

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

        public static bool IsValid(this NullForgivingContext context)
        {
            return context == NullForgivingContext.General
                   || context == NullForgivingContext.Lambda
                   || context == NullForgivingContext.NullOrDefault;
        }

        public static NullForgivingContext GetContext(this PostfixUnaryExpressionSyntax node)
        {
            if (node.IsNullOrDefaultExpression())
            {
                return NullForgivingContext.NullOrDefault;
            }

            if (node.IsInsideLambdaExpression())
            {
                return NullForgivingContext.Lambda;
            }

            return NullForgivingContext.General;
        }
    }
}