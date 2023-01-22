namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal interface ISyntaxTreeAnalyzer
    {
        Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(AnalysisContext analysisContext, CancellationToken cancellationToken);
    }
}