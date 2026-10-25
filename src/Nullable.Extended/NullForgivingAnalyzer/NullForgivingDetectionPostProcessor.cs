using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Nullable.Extended.AnalyzerFramework;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    [Export(typeof(ISyntaxAnalysisPostProcessor))]
    internal class NullForgivingDetectionPostProcessor : ISyntaxAnalysisPostProcessor
    {
        private const string FirstNullableDiagnostic = "CS8600";
        private const string LastNullableDiagnostic = "CS8900";

        public async Task PostProcessAsync(DocumentContext documentContext, ProjectContext projectContext, CancellationToken cancellationToken)
        {
            var document = documentContext.Document;
            var syntaxRoot = documentContext.SyntaxRoot;
            var diagnosticLocations = projectContext.DiagnosticLocations;

            foreach (var analysisResult in documentContext.Results.OfType<NullForgivingDocumentResult>())
            {
                var node = analysisResult.Node;
                var rewrittenSyntaxRoot = syntaxRoot.ReplaceNode(node, RewriteNullForgivingNode(node));
                var allDiagnostics = await projectContext.GetDiagnosticsAsync(documentContext, rewrittenSyntaxRoot, GetAnalysisScope(node), cancellationToken);

                bool IsNewDiagnosticInCurrentDocument(Diagnostic d)
                {
                    var span = d.Location.GetLineSpan();
                    return span.Path == document.FilePath && !diagnosticLocations.Contains(span);
                }

                var newNullableDiagnostics = allDiagnostics
                    .Where(d => !d.IsSuppressed)
                    .Where(IsNullableDiagnostic)
                    .Where(IsNewDiagnosticInCurrentDocument);

                analysisResult.SetIsRequired(newNullableDiagnostics.Any());
            }
        }

        public bool IsSpecificDiagnostic(Diagnostic diagnostic)
        {
            return IsNullableDiagnostic(diagnostic);
        }

        private static bool IsNullableDiagnostic(Diagnostic d)
        {
            return IsNullableDiagnosticId(d.Id);
        }

        private static bool IsNullableDiagnosticId(string id)
        {
            return string.Compare(id, FirstNullableDiagnostic, StringComparison.OrdinalIgnoreCase) >= 0
                && string.Compare(id, LastNullableDiagnostic, StringComparison.OrdinalIgnoreCase) <= 0;
        }

        // Nullable flow analysis works per method body, so only the diagnostics of the enclosing body can change.
        // Fields, initializers and constructors can also cause diagnostics at other members of the type, e.g. CS8618, so use the type for them.
        private static TextSpan? GetAnalysisScope(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case MethodDeclarationSyntax:
                    case AccessorDeclarationSyntax:
                    case OperatorDeclarationSyntax:
                    case ConversionOperatorDeclarationSyntax:
                    case DestructorDeclarationSyntax:
                    case BaseTypeDeclarationSyntax:
                        return ancestor.FullSpan;
                    case GlobalStatementSyntax:
                        return null;
                }
            }

            return null;
        }

        private static SyntaxNode RewriteNullForgivingNode(PostfixUnaryExpressionSyntax node)
        {
            var operatorToken = node.OperatorToken;

            // Replace the operator by a space instead of removing it, so the positions of all following tokens stay the same.
            var trailingTrivia = node.Operand.GetTrailingTrivia()
                .AddRange(operatorToken.LeadingTrivia)
                .Add(SyntaxFactory.Space)
                .AddRange(operatorToken.TrailingTrivia);

            return node.Operand.WithTrailingTrivia(trailingTrivia);
        }
    }
}
