using System.Composition;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    [Export(typeof(IAnalyzerEngine))]
    internal class AnalyzerEngine(IEnumerable<ISyntaxTreeAnalyzer> syntaxTreeAnalyzers, IEnumerable<ISyntaxAnalysisPostProcessor> postProcessors) : IAnalyzerEngine
    {
        private readonly IReadOnlyCollection<ISyntaxTreeAnalyzer> _syntaxTreeAnalyzers = syntaxTreeAnalyzers.ToArray();
        private readonly IReadOnlyCollection<ISyntaxAnalysisPostProcessor> _postProcessors = postProcessors.ToArray();

        public AnalysisSession CreateSession(IEnumerable<Document> documents, CancellationToken cancellationToken)
        {
            return new AnalysisSession(documents.ToArray(), _syntaxTreeAnalyzers, _postProcessors, cancellationToken);
        }
    }
}
