using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal interface ISyntaxAnalysisPostProcessor
    {
        Task PostProcessAsync(Project project, Document document, SyntaxNode syntaxRoot, ICollection<FileLinePositionSpan> diagnosticLocations,
            Func<Compilation, Task<ImmutableArray<Diagnostic>>> getDiagnosticsAsync, IReadOnlyCollection<AnalysisResult> analysisResults);

        bool IsSpecificDiagnostic(Diagnostic diagnostic, IReadOnlyCollection<AnalysisResult> results);
    }
}