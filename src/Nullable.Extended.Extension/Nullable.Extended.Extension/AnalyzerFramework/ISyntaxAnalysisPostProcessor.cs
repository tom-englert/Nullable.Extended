using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal interface ISyntaxAnalysisPostProcessor
    {
        Task PostProcessAsync(Project project, IReadOnlyCollection<AnalysisResult> analysisResults);
    }
}