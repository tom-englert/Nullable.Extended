using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Shared;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    internal static class ExtensionMethods
    {
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
