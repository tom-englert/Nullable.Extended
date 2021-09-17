using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Nullable.Extended.Extension.AnalyzerFramework;

using AnalysisResult = Nullable.Extended.Extension.AnalyzerFramework.AnalysisResult;

namespace Nullable.Extended.Extension.NullForgivingAnalyzer
{
    [Export(typeof(ISyntaxAnalysisPostProcessor))]
    internal class NullForgivingDetectionPostProcessor : ISyntaxAnalysisPostProcessor
    {
        private const string FirstNullableDiagnostic = "CS8600";
        private const string LastNullableDiagnostic = "CS8900";

        public async Task PostProcessAsync(Project project, IReadOnlyCollection<AnalysisResult> analysisResults)
        {
            var nullForgivingAnalysisResults = analysisResults
                .OfType<NullForgivingAnalysisResult>()
                .ToArray();

            try
            {
                var analyzers = project.AnalyzerReferences
                    .SelectMany(r => r.GetAnalyzers(LanguageNames.CSharp))
                    .OfType<DiagnosticSuppressor>()
                    .Cast<DiagnosticAnalyzer>()
                    .ToImmutableArray();

                async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Compilation compilation)
                {
                    return analyzers.Any()
                        ? await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync()
                        : compilation.GetDiagnostics();
                }

                var originalCompilation = await project.GetCompilationAsync() ?? throw new InvalidOperationException("Error getting compilation of project");
                var originalDiagnostics = await GetDiagnosticsAsync(originalCompilation);

                ThrowOnCompilationErrors(originalDiagnostics);

                var originalNullableDiagnosticLocations = new HashSet<FileLinePositionSpan>(originalDiagnostics
                    .Where(d => !d.IsSuppressed)
                    .Where(IsNullableDiagnostic)
                    .Select(d => d.Location.GetLineSpan()));

                foreach (var resultsByDocument in nullForgivingAnalysisResults.GroupBy(r => r.AnalysisContext.Document))
                {
                    var document = resultsByDocument.Key;
                    var originalSyntaxRoot = await document.GetSyntaxRootAsync() ?? throw new InvalidOperationException("Error getting syntax root of document");

                    foreach (var analysisResult in resultsByDocument)
                    {
                        var node = analysisResult.Node;
                        var syntaxRoot = originalSyntaxRoot.ReplaceNode(node, RewriteNullForgivingNode(node));
                        var compilation = await project
                            .RemoveDocument(document.Id)
                            .AddDocument(document.Name, syntaxRoot, document.Folders, document.FilePath)
                            .Project
                            .GetCompilationAsync() ?? throw new InvalidOperationException("Error getting compilation of project");

                        var allDiagnostics = await GetDiagnosticsAsync(compilation);

                        ThrowOnCompilationErrors(allDiagnostics);

                        bool IsNewDiagnosticInCurrentDocument(Diagnostic d)
                        {
                            var span = d.Location.GetLineSpan();
                            return span.Path == document.FilePath && !originalNullableDiagnosticLocations.Contains(span);
                        }

                        var newNullableDiagnostics = allDiagnostics
                            .Where(d => !d.IsSuppressed)
                            .Where(IsNullableDiagnostic)
                            .Where(IsNewDiagnosticInCurrentDocument);

                        if (newNullableDiagnostics.Any())
                        {
                            analysisResult.IsRequired = true;
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                SetAllInvalid(nullForgivingAnalysisResults);
            }
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

        private static void SetAllInvalid(IReadOnlyCollection<NullForgivingAnalysisResult> items)
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

        private static void ThrowOnCompilationErrors(IEnumerable<Diagnostic> diagnostics)
        {
            if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !IsNullableDiagnostic(diagnostic)))
                throw new InvalidOperationException("Compilation has errors");


        }
    }
}
