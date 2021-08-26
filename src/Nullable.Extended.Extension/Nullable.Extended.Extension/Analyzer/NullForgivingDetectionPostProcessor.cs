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
using TomsToolbox.Essentials;
using AnalysisResult = Nullable.Extended.Extension.AnalyzerFramework.AnalysisResult;

namespace Nullable.Extended.Extension.Analyzer
{
    [Export(typeof(ISyntaxAnalysisPostProcessor))]
    internal class NullForgivingDetectionPostProcessor : ISyntaxAnalysisPostProcessor
    {
        private const string FirstNullableDiagnostic = "CS8600";
        private const string LastNullableDiagnostic = "CS8900";

        private static readonly IEqualityComparer<SyntaxNode> SyntaxNodeEqualityComparer = new DelegateEqualityComparer<SyntaxNode>(a => a!.FullSpan);

        public async Task<IReadOnlyCollection<AnalysisResult>> PostProcessAsync(Project project, IEnumerable<AnalysisResult> analysisResults)
        {
            var nullForgivingAnalysisResults = analysisResults
                .OfType<NullForgivingAnalysisResult>()
                .ToArray();

            var resultsBySyntaxRoot = new Dictionary<SyntaxNode, NullForgivingAnalysisResult[]>(SyntaxNodeEqualityComparer);

            project = await RewriteSyntaxTreesAsync(project, nullForgivingAnalysisResults, resultsBySyntaxRoot);

            var compilation = await project.GetCompilationAsync();

            if (compilation == null)
            {
                return WithAllAsInvalid(nullForgivingAnalysisResults);
            }

            var analyzers = project.AnalyzerReferences
                .SelectMany(r => r.GetAnalyzers(LanguageNames.CSharp))
                .OfType<DiagnosticSuppressor>()
                .Cast<DiagnosticAnalyzer>()
                .ToImmutableArray();

            var allDiagnostics = analyzers.Any()
                ? await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync()
                : compilation.GetDiagnostics();

            if (allDiagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !IsNullableDiagnostic(diagnostic)))
            {
                return WithAllAsInvalid(nullForgivingAnalysisResults);
            }

            var nullableDiagnostics = allDiagnostics
                .Where(d => !d.IsSuppressed)
                .Where(IsNullableDiagnostic);

            foreach (var diagnostic in nullableDiagnostics)
            {
                var sourceTree = diagnostic.Location.SourceTree;
                if (sourceTree == null)
                    continue;

                var syntaxRoot = await sourceTree.GetRootAsync();

                if (!resultsBySyntaxRoot.TryGetValue(syntaxRoot, out var results))
                    continue;

                bool IsAffectedResult(NullForgivingAnalysisResult result)
                {
                    var resultLocation = result.Node.Operand.GetLocation();
                    var diagnosticLocation = diagnostic.Location;
                    var intersection = diagnosticLocation.SourceSpan.Intersection(resultLocation.SourceSpan);

                    return intersection == resultLocation.SourceSpan;
                }

                var affectedResults = results.Where(IsAffectedResult);

                foreach (var affectedResult in affectedResults)
                {
                    affectedResult.IsRequired = true;
                }
            }

            return nullForgivingAnalysisResults;
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

        private static IReadOnlyCollection<AnalysisResult> WithAllAsInvalid(IReadOnlyCollection<NullForgivingAnalysisResult> items)
        {
            foreach (var item in items)
            {
                item.Context = NullForgivingContext.Invalid;
            }

            return items;
        }

        private static async Task<Project> RewriteSyntaxTreesAsync(Project project, IEnumerable<NullForgivingAnalysisResult> analysisResults, IDictionary<SyntaxNode, NullForgivingAnalysisResult[]> resultsBySyntaxRoot)
        {
            var resultsByDocument = analysisResults.GroupBy(r => r.AnalysisContext.Document);

            foreach (var documentResults in resultsByDocument)
            {
                var document = documentResults.Key;
                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                    continue;

                root = root.ReplaceNodes(documentResults.Select(r => r.Node), (originalNode, updatedNode) =>
                {
                    var sourceCode = updatedNode.ToFullString().ReplaceNullForgivingToken();
                    return SyntaxFactory.ParseExpression(sourceCode);
                });

                resultsBySyntaxRoot.Add(root, documentResults.ToArray());

                project = project
                    .RemoveDocument(document.Id)
                    .AddDocument(document.Name, root, document.Folders, document.FilePath).Project;
            }

            return project;
        }
    }
}
