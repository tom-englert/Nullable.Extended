using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal interface ISyntaxTreeAnalyzer
    {
        Task<IEnumerable<AnalysisResult>> AnalyzeAsync(AnalysisContext analysisContext);
        
    }
}