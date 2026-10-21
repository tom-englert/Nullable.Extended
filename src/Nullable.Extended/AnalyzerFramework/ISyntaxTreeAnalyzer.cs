namespace Nullable.Extended.AnalyzerFramework
{
    internal interface ISyntaxTreeAnalyzer
    {
        Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(DocumentContext documentContext, CancellationToken cancellationToken);
    }
}
