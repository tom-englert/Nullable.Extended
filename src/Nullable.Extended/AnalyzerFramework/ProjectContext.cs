using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nullable.Extended.AnalyzerFramework
{
    /// <summary>
    /// The original diagnostics of a project, i.e. the state of the project before any post processor modified a document.
    /// This is independent of the analysis results of individual documents, so it can be calculated once and shared by all documents of the project.
    /// </summary>
    internal sealed class ProjectContext
    {
        private readonly ImmutableArray<DiagnosticAnalyzer> _analyzers;
        private readonly HashSet<string> _errorFilePaths;

        private ProjectContext(Project project, ImmutableArray<DiagnosticAnalyzer> analyzers, ImmutableArray<Diagnostic> diagnostics, HashSet<string> errorFilePaths)
        {
            Project = project;
            _analyzers = analyzers;
            _errorFilePaths = errorFilePaths;
            DiagnosticLocations = new HashSet<FileLinePositionSpan>(diagnostics.Select(d => d.Location.GetLineSpan()));
        }

        public Project Project { get; }

        public ICollection<FileLinePositionSpan> DiagnosticLocations { get; }

        public bool HasErrors(string? filePath)
        {
            return filePath != null && _errorFilePaths.Contains(filePath);
        }

        /// <summary>
        /// Gets the diagnostics of a document with its syntax root replaced by <paramref name="modifiedRoot"/>, all other documents unchanged.
        /// Without suppressors only the diagnostics inside <paramref name="scope"/> are calculated, or of the whole document if no scope is specified.
        /// </summary>
        public async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(DocumentContext documentContext, SyntaxNode modifiedRoot, TextSpan? scope, CancellationToken cancellationToken)
        {
            var compilation = await Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                              ?? throw new InvalidOperationException("Error getting compilation of project");

            var originalTree = documentContext.SyntaxTree;
            var modifiedTree = originalTree.WithRootAndOptions(modifiedRoot, originalTree.Options);
            var modifiedCompilation = compilation.ReplaceSyntaxTree(originalTree, modifiedTree);

            // Suppressors work on the diagnostics of the whole compilation, so only without suppressors the analysis can be limited to the modified tree.
            return _analyzers.Any()
                ? await modifiedCompilation.WithAnalyzers(_analyzers).GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false)
                : modifiedCompilation.GetSemanticModel(modifiedTree).GetDiagnostics(scope, cancellationToken);
        }

        public static async Task<ProjectContext> CreateAsync(Project project, IEnumerable<ISyntaxAnalysisPostProcessor> postProcessors, CancellationToken cancellationToken)
        {
            var analyzers = project.AnalyzerReferences
                .SelectMany(r => r.GetAnalyzers(LanguageNames.CSharp))
                .OfType<DiagnosticSuppressor>()
                .Cast<DiagnosticAnalyzer>()
                .ToImmutableArray();

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                              ?? throw new InvalidOperationException("Error getting compilation of project");

            var diagnostics = analyzers.Any()
                ? await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false)
                : compilation.GetDiagnostics(cancellationToken);

            var errorFilePaths = new HashSet<string>(
                diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                                         && !diagnostic.IsSuppressed
                                         && !postProcessors.Any(processor => processor.IsSpecificDiagnostic(diagnostic)))
                    .Select(diagnostic => diagnostic.Location.GetLineSpan().Path),
                StringComparer.OrdinalIgnoreCase);

            return new ProjectContext(project, analyzers, diagnostics, errorFilePaths);
        }
    }
}
