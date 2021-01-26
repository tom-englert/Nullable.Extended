using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nullable.Extended.Extension.Analyzer;
using TomsToolbox.Composition;
using TomsToolbox.Essentials;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    [Export(typeof(IAnalyzerEngine))]
    class AnalyzerEngine : IAnalyzerEngine
    {
        private static readonly string[] NullableDiagnostics = { "CS8602", "CS8603", "CS8604" };

        private readonly IEnumerable<ISyntaxTreeAnalyzer> _syntaxTreeAnalyzers;

        [ImportingConstructor]
        public AnalyzerEngine(IExportProvider exportProvider)
        {
            _syntaxTreeAnalyzers = exportProvider.GetExportedValues<ISyntaxTreeAnalyzer>();
        }

        public async Task<IEnumerable<AnalysisResult>> AnalyzeAsync(IEnumerable<Document> documents)
        {
            var documentsByProject = documents.GroupBy(document => document.Project);

            var results = await Task
                .WhenAll(documentsByProject.Select(AnalyzeAsync))
                .ConfigureAwait(false);

            return results.SelectMany(r => r).ToImmutableArray();
        }

        public async Task<IEnumerable<AnalysisResult>> AnalyzeAsync(IGrouping<Project, Document> documents)
        {
            var project = documents.Key;

            var tasks = documents.Select(AnalyzeAsync).ToImmutableArray();

            var analysisResults = (await Task.WhenAll(tasks).ConfigureAwait(false))
                .SelectMany(r => r)
                .ToList();

            return await PostProcessAsync(project, analysisResults).ConfigureAwait(false);
        }

        private static async Task<IEnumerable<AnalysisResult>> PostProcessAsync(Project project, IReadOnlyCollection<AnalysisResult> analysisResults)
        {
            var resultsBySyntaxRoot = new Dictionary<SyntaxNode, AnalysisResult[]>(new DelegateEqualityComparer<SyntaxNode>(a => a.FullSpan));

            project = await RewriteSyntaxTreesAsync(project, analysisResults, resultsBySyntaxRoot).ConfigureAwait(false);

            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);

            if (compilation == null)
            {
                return WithAllInvalid(analysisResults);
            }

            var allDiagnostics = compilation.GetDiagnostics();

            if (allDiagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                return WithAllInvalid(analysisResults);
            }

            var diagnostics = allDiagnostics
                .Where(d => !d.IsSuppressed)
                .Where(d => NullableDiagnostics.Contains(d.Id));

            foreach (var diagnostic in diagnostics)
            {
                var sourceTree = diagnostic.Location.SourceTree;
                if (sourceTree == null)
                    continue;

                var syntaxRoot = await sourceTree.GetRootAsync().ConfigureAwait(false);

                if (!resultsBySyntaxRoot.TryGetValue(syntaxRoot, out var results))
                    continue;

                var affectedResult = results.FirstOrDefault(result =>
                    diagnostic.Location.GetLineSpan().EndLinePosition == result.Position.StartLinePosition);

                if (affectedResult != null)
                {
                    affectedResult.IsRequired = true;
                }
            }

            return analysisResults;
        }

        private static IEnumerable<AnalysisResult> WithAllInvalid(IReadOnlyCollection<AnalysisResult> items)
        {
            foreach (var item in items)
            {
                item.Context = NullForgivingContext.Invalid;
            }

            return items;
        }

        private static async Task<Project> RewriteSyntaxTreesAsync(Project project, IReadOnlyCollection<AnalysisResult> analysisResults, Dictionary<SyntaxNode, AnalysisResult[]> resultsBySyntaxRoot)
        {
            var resultsByDocument = analysisResults.GroupBy(r => r.AnalysisContext.Document);

            foreach (var documentResults in resultsByDocument)
            {
                var document = documentResults.Key;
                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                    continue;

                root = root.ReplaceNodes(documentResults.Select(r => r.StartingToken), (a, token) =>
                {
                    var sourceCode = token.ToString().TrimEnd('!') + " ";
                    var newExpression = SyntaxFactory.ParseExpression(sourceCode);
                    return newExpression;
                });

                resultsBySyntaxRoot.Add(root, documentResults.ToArray());

                project = project.RemoveDocument(document.Id)
                    .AddDocument(document.Name, root, document.Folders, document.FilePath).Project;
            }

            return project;
        }

        private Task<IEnumerable<AnalysisResult>> AnalyzeAsync(Document document)
        {
            return Task.Run(async () =>
            {
                var syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);

                if (syntaxTree == null || syntaxTree.BeginsWithAutoGeneratedComment())
                    return Enumerable.Empty<AnalysisResult>();

                var tasks = _syntaxTreeAnalyzers
                    .Select(analyzer => analyzer.AnalyzeAsync(syntaxTree, new AnalysisContext(document)))
                    .ToImmutableArray();

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                return results.SelectMany(r => r);
            });
        }
    }
}