using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal interface ISyntaxAnalysisPostProcessor
    {
        Task PostProcessAsync(Project project, Document document, SyntaxNode syntaxRoot, ICollection<FileLinePositionSpan> diagnosticLocations,
            Func<Compilation, Task<ImmutableArray<Diagnostic>>> getDiagnosticsAsync,
            IEnumerable<AnalysisResult> analysisResults, CancellationToken cancellationToken);

        bool IsSpecificDiagnostic(Diagnostic diagnostic, IReadOnlyCollection<AnalysisResult> results);
    }
}