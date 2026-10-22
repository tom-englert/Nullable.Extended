using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

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

        public async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Compilation compilation, CancellationToken cancellationToken)
        {
            return _analyzers.Any()
                ? await compilation.WithAnalyzers(_analyzers).GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false)
                : compilation.GetDiagnostics(cancellationToken);
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
