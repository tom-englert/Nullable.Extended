using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.Extension.AnalyzerFramework;

namespace Nullable.Extended.Extension.Analyzer
{
    [Export(typeof(ISyntaxTreeAnalyzer))]
    internal class NullForgivingDetectionAnalyzer : ISyntaxTreeAnalyzer
    {
        public async Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(AnalysisContext analysisContext)
        {
            var root = analysisContext.SyntaxRoot;

            var items = root
                .DescendantNodesAndSelf()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                .Select(item => MapResult(analysisContext, item))
                .ToArray();

            return await Task.FromResult(items);
        }

        private static AnalysisResult MapResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax node)
        {
            return new NullForgivingAnalysisResult(analysisContext, node, node.GetContext());
        }
    }
}
