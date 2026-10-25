namespace Nullable.Extended.AnalyzerFramework
{
    internal interface ISyntaxTreeAnalyzer
    {
        Task<IReadOnlyCollection<DocumentAnalysisResult>> AnalyzeAsync(DocumentContext documentContext, CancellationToken cancellationToken);
    }
}
