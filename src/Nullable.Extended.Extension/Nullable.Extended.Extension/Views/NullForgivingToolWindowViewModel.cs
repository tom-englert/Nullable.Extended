using System.Composition;

using Nullable.Extended.NullForgivingAnalyzer;

using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport(nameof(NullForgivingToolWindow))]
    [Shared]
    internal class NullForgivingToolWindowViewModel(AnalyzerViewModel analyzerViewModel) : AnalyzerResultViewModel<NullForgivingAnalysisResult>(analyzerViewModel);
}
