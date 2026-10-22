using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    internal interface ISyntaxAnalysisPostProcessor
    {
        /// <summary>
        /// Post processes the <see cref="DocumentContext.Results"/> of the document.
        /// Throwing an exception marks all results of the document as having compilation errors.
        /// </summary>
        Task PostProcessAsync(DocumentContext documentContext, ProjectContext projectContext, CancellationToken cancellationToken);

        /// <summary>
        /// Determines whether the diagnostic is specific to this post processor, i.e. is expected and must not be treated as an error that prevents post processing.
        /// This must not depend on the analysis results, because it is evaluated once per project.
        /// </summary>
        bool IsSpecificDiagnostic(Diagnostic diagnostic);
    }
}
