using System.Composition;

using Nullable.Extended.Extension.AnalyzerFramework;
using Nullable.Extended.Extension.NullForgivingAnalyzer;

using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport(nameof(NullForgivingToolWindow))]
    [Shared]
    internal class NullForgivingToolWindowViewModel : AnalyzerResultViewModel<NullForgivingAnalysisResult>
    {
        public NullForgivingToolWindowViewModel(AnalyzerViewModel analyzerViewModel)
            : base(analyzerViewModel)
        {
        }
    }
}
