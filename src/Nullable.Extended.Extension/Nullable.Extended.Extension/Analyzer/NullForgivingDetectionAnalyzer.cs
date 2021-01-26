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
        public async Task<IEnumerable<AnalysisResult>> AnalyzeAsync(SyntaxTree syntaxTree, AnalysisContext analysisContext)
        {
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            var text = root.ToFullString();

            var items = root
                .DescendantNodesAndSelf()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression));

            var results = items.Select(item => MapResult(syntaxTree, analysisContext, item, text));

            return results.ToImmutableArray();
        }

        private static AnalysisResult MapResult(SyntaxTree syntaxTree, AnalysisContext analysisContext, PostfixUnaryExpressionSyntax node, string text)
        {
            return new AnalysisResult(analysisContext, syntaxTree.FilePath, node, GetContext(node), text.Substring(0, node.Span.End));
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
