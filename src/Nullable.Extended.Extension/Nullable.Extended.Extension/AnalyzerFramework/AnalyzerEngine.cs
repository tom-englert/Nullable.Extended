using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using TomsToolbox.Composition;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    [Export(typeof(IAnalyzerEngine))]
    internal class AnalyzerEngine : IAnalyzerEngine
    {
        private readonly ICollection<ISyntaxTreeAnalyzer> _syntaxTreeAnalyzers;
        private readonly ICollection<ISyntaxAnalysisPostProcessor> _postProcessors;

        public AnalyzerEngine(IExportProvider exportProvider)
        {
            _syntaxTreeAnalyzers = exportProvider.GetExportedValues<ISyntaxTreeAnalyzer>().ToArray();
            _postProcessors = exportProvider.GetExportedValues<ISyntaxAnalysisPostProcessor>().ToArray();
        }

        public async Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(IEnumerable<Document> documents, CancellationToken cancellationToken)
        {
            var documentTasks = documents.Select(doc => AnalyzeDocumentAsync(doc, cancellationToken));

            var analysisResults = (await Task.WhenAll(documentTasks))
                .SelectMany(r => r)
                .ToList()
                .AsReadOnly();

            var resultsByProject = analysisResults.GroupBy(result => result.AnalysisContext.Document.Project);

            var projectTasks = resultsByProject.Select(results => PostProcessProjectAsync(results, cancellationToken));

            await Task.WhenAll(projectTasks);

            analysisResults = analysisResults
                .GroupBy(r => r.Position)
                .Select(g => g.OrderBy(r => r).First())
                .ToList()
                .AsReadOnly();

            return analysisResults;
        }

        private Task PostProcessProjectAsync(IGrouping<Project, AnalysisResult> analysisResults, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                if (!_postProcessors.Any())
                    return;

                try
                {
                    var project = analysisResults.Key;

                    IReadOnlyCollection<AnalysisResult> results = analysisResults.ToArray();

                    var analyzers = project.AnalyzerReferences
                        .SelectMany(r => r.GetAnalyzers(LanguageNames.CSharp))
                        .OfType<DiagnosticSuppressor>()
                        .Cast<DiagnosticAnalyzer>()
                        .ToImmutableArray();

                    async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Compilation compilation)
                    {
                        return analyzers.Any()
                            ? await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync(cancellationToken)
                            : compilation.GetDiagnostics();
                    }

                    var compilation = await project.GetCompilationAsync(cancellationToken) ?? throw new InvalidOperationException("Error getting compilation of project");
                    var diagnostics = await GetDiagnosticsAsync(compilation);
                    var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                                                                 && !diagnostic.IsSuppressed
                                                                 && !_postProcessors.Any(processor => processor.IsSpecificDiagnostic(diagnostic, results)))
                        .ToImmutableArray();

                    var diagnosticLocations = new HashSet<FileLinePositionSpan>(diagnostics.Select(d => d.Location.GetLineSpan()));

                    foreach (var resultsByContext in results.GroupBy(r => r.AnalysisContext))
                    {
                        try
                        {
                            var context = resultsByContext.Key;
                            var document = context.Document;
                            var filePath = document.FilePath;
                            var syntaxRoot = context.SyntaxRoot;

                            if (errors.Any(diagnostic => string.Equals(diagnostic.Location.GetLineSpan().Path, filePath, StringComparison.OrdinalIgnoreCase)))
                                throw new InvalidOperationException("Document has errors");

                            foreach (var analyzer in _postProcessors)
                            {
                                await analyzer.PostProcessAsync(project, document, syntaxRoot, diagnosticLocations, GetDiagnosticsAsync, resultsByContext, cancellationToken);
                            }
                        }
                        catch
                        {
                            foreach (var result in resultsByContext)
                            {
                                result.HasCompilationErrors = true;
                            }
                        }
                    }
                }
                catch
                {
                    foreach (var result in analysisResults)
                    {
                        result.HasCompilationErrors = true;
                    }
                }
            }, cancellationToken);
        }

        private Task<IReadOnlyCollection<AnalysisResult>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                if (syntaxTree == null)
                    return (IReadOnlyCollection<AnalysisResult>)Array.Empty<AnalysisResult>();

                var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);
                if (syntaxRoot.BeginsWithAutoGeneratedComment())
                    return Array.Empty<AnalysisResult>();

                var analysisContext = new AnalysisContext(document, syntaxTree, syntaxRoot);

                var tasks = _syntaxTreeAnalyzers
                    .Select(analyzer => analyzer.AnalyzeAsync(analysisContext, cancellationToken));

                var results = await Task.WhenAll(tasks);

                return results
                    .SelectMany(r => r)
                    .ToArray();
            }, cancellationToken);
        }
    }
}