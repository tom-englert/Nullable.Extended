using System;
using System.Collections.Generic;
using Nullable.Extended.Extension.AnalyzerFramework;

namespace Nullable.Extended.Extension.Views
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