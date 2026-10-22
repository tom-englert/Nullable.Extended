using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    public interface IAnalyzerEngine
    {
        /// <summary>
        /// Creates a session to analyze the documents. Nothing happens until <see cref="AnalysisSession.StartAsync"/> is called;
        /// then the documents are analyzed and the results are post-processed in the background.
        /// Await <see cref="AnalysisSession.StartAsync"/> to wait for the analysis, use <see cref="AnalysisSession.Completion"/> to await the post-processing,
        /// and <see cref="DocumentContext.Cancel"/> to cancel the post-processing of a single document.
        /// </summary>
        AnalysisSession CreateSession(IEnumerable<Document> documents, CancellationToken cancellationToken);
    }
}
