using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Nullable.Extended.Extension.AnalyzerFramework;

namespace Nullable.Extended.Extension.NullForgivingAnalyzer
{
    [Export(typeof(ISyntaxAnalysisPostProcessor))]
    internal class NullForgivingDetectionPostProcessor : ISyntaxAnalysisPostProcessor
    {
        private const string FirstNullableDiagnostic = "CS8600";
        private const string LastNullableDiagnostic = "CS8900";

        public async Task PostProcessAsync(Project project, Document document, SyntaxNode syntaxRoot, 
            ICollection<FileLinePositionSpan> diagnosticLocations,
            Func<Compilation, Task<ImmutableArray<Diagnostic>>> getDiagnosticsAsync, 
            IEnumerable<AnalysisResult> analysisResults, 
            CancellationToken cancellationToken)
        {
            var nullForgivingAnalysisResults = analysisResults
                .OfType<NullForgivingAnalysisResult>()
                .ToList()
                .AsReadOnly();

            try
            {
                foreach (var analysisResult in nullForgivingAnalysisResults)
                {
                    var node = analysisResult.Node;
                    var rewrittenSyntaxRoot = syntaxRoot.ReplaceNode(node, RewriteNullForgivingNode(node));
                    // var sourceCode = rewrittenSyntaxRoot.GetText().ToString();
                    var compilation = await project
                        .RemoveDocument(document.Id)
                        .AddDocument(document.Name, rewrittenSyntaxRoot, document.Folders, document.FilePath)
                        .Project
                        .GetCompilationAsync(cancellationToken) ?? throw new InvalidOperationException("Error getting compilation of project");

                    var allDiagnostics = await getDiagnosticsAsync(compilation);

                    bool IsNewDiagnosticInCurrentDocument(Diagnostic d)
                    {
                        var span = d.Location.GetLineSpan();
                        return span.Path == document.FilePath && !diagnosticLocations.Contains(span);
                    }

                    var newNullableDiagnostics = allDiagnostics
                        .Where(d => !d.IsSuppressed)
                        .Where(IsNullableDiagnostic)
                        .Where(IsNewDiagnosticInCurrentDocument);

                    analysisResult.IsRequired = newNullableDiagnostics.Any();
                }
            }
            catch (InvalidOperationException)
            {
                SetAllInvalid(nullForgivingAnalysisResults);
            }
        }

        public bool IsSpecificDiagnostic(Diagnostic diagnostic, IReadOnlyCollection<AnalysisResult> results)
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

        private static void SetAllInvalid(IEnumerable<NullForgivingAnalysisResult> items)
        {
            foreach (var item in items)
            {
                item.Context = NullForgivingContext.Invalid;
            }
        }

        private static SyntaxNode RewriteNullForgivingNode(SyntaxNode n)
        {
            var sourceCode = n.ToFullString().ReplaceNullForgivingToken();
            return SyntaxFactory.ParseExpression(sourceCode);
        }
    }
}
