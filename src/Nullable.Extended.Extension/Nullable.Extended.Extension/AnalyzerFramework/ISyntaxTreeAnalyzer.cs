using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal interface ISyntaxTreeAnalyzer
    {
        Task<IEnumerable<AnalysisResult>> AnalyzeAsync(SyntaxTree syntaxTree, AnalysisContext analysisContext);
    }
}