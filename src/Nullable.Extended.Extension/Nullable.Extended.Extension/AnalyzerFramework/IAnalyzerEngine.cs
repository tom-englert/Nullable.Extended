using Microsoft.CodeAnalysis;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    public interface IAnalyzerEngine
    {
        Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(IEnumerable<Document> documents, CancellationToken cancellationToken);
    }
}
