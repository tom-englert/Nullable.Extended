using System;
using System.Collections.Generic;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal class AnalysisResultsChangedArgs : EventArgs
    {
        public AnalysisResultsChangedArgs(IReadOnlyList<AnalysisResult> results)
        {
            Results = results;
        }

        public IReadOnlyList<AnalysisResult> Results { get; }
    }
}