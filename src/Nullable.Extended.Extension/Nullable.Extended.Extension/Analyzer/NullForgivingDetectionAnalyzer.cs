using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Nullable.Extended.Extension.AnalyzerFramework;
using AnalysisContext = Nullable.Extended.Extension.AnalyzerFramework.AnalysisContext;
using AnalysisResult = Nullable.Extended.Extension.AnalyzerFramework.AnalysisResult;

namespace Nullable.Extended.Extension.Analyzer
{
    [Export(typeof(ISyntaxTreeAnalyzer))]
    class NullForgivingDetectionAnalyzer : ISyntaxTreeAnalyzer
    {
        public async Task<IEnumerable<AnalysisResult>> AnalyzeAsync(AnalysisContext analysisContext)
        {
            var root = analysisContext.SyntaxRoot;

            var items = root
                .DescendantNodesAndSelf()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                .ToImmutableList();

            var results = items.Select(item => MapResult(analysisContext, item));

            return results.ToImmutableArray();
        }

        private static AnalysisResult MapResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax node)
        {
            return new AnalysisResult(analysisContext, node, GetContext(node));
        }

        private static NullForgivingContext GetContext(PostfixUnaryExpressionSyntax node)
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

    internal static class ExtensionMethods
    {
        public static IEnumerable<SyntaxNode> SelfAndDescendants(this SyntaxNode? node)
        {
            while (node != null)
            {
                yield return node;

                node = node.Parent;
            }
        }
    }
}
