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

        /// <summary>
        /// Creates a session to analyze the documents, optionally without the post-processing.
        /// Without post-processing the analysis is purely syntactic, no compilation or semantic model is needed, and the results are not evaluated any further,
        /// e.g. <see cref="NullForgivingAnalyzer.NullForgivingAnalysisResult.IsRequired"/> stays <c>null</c>.
        /// </summary>
        /// <param name="documents">The documents to analyze.</param>
        /// <param name="postProcess"><c>true</c> to post-process the results in the background, <c>false</c> to only analyze the syntax.</param>
        /// <param name="cancellationToken">The token to cancel the session.</param>
        AnalysisSession CreateSession(IEnumerable<Document> documents, bool postProcess, CancellationToken cancellationToken);
    }
}
