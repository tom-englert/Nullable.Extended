using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.AnalyzerFramework;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    [Export(typeof(ISyntaxTreeAnalyzer))]
    internal class NullForgivingDetectionAnalyzer : ISyntaxTreeAnalyzer
    {
        public async Task<IReadOnlyCollection<DocumentAnalysisResult>> AnalyzeAsync(DocumentContext documentContext, CancellationToken cancellationToken)
        {
            var root = documentContext.SyntaxRoot;

            var items = root
                .DescendantNodesAndSelf()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                .Select(item => MapResult(documentContext, item))
                .ToArray();

            return await Task.FromResult(items);
        }

        private static DocumentAnalysisResult MapResult(DocumentContext documentContext, PostfixUnaryExpressionSyntax node)
        {
            return new NullForgivingDocumentResult(documentContext, node, node.GetContext());
        }
    }
}
