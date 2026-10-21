using System.ComponentModel;
using System.Windows.Input;

using Microsoft.VisualStudio.Shell;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.Extension.AnalyzerFramework;

using TomsToolbox.Wpf;

namespace Nullable.Extended.Extension.Views
{
    internal abstract partial class AnalyzerResultViewModel<TResult> : INotifyPropertyChanged where TResult : AnalysisResult
    {
        protected AnalyzerResultViewModel(AnalyzerViewModel analyzerViewModel)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AnalyzerViewModel = analyzerViewModel;
            SetResults(analyzerViewModel.AnalysisResults);
            AnalyzerViewModel.AnalysisResultsChanged += AnalyzerViewModel_AnalysisResultsChanged;
        }

        private void AnalyzerViewModel_AnalysisResultsChanged(object sender, AnalysisResultsChangedArgs e)
        {
            SetResults(e.Results);
        }

        private void SetResults(IEnumerable<AnalysisResult> results)
        {
            AnalysisResults = results
                .OfType<TResult>()
                .ToList()
                .AsReadOnly();
        }

        public AnalyzerViewModel AnalyzerViewModel { get; }

        public IReadOnlyList<TResult> AnalysisResults { get; private set; } = [];

        public ICommand AnalyzeCommand => new DelegateCommand(CanAnalyze, AnalyzeSolution);

        public static ICommand OpenInDocumentCommand => new DelegateCommand<AnalysisResult>(result => result.OpenInDocument());

        private bool CanAnalyze()
        {
            return AnalyzerViewModel.CanAnalyze;
        }

        private void AnalyzeSolution()
        {
            AnalyzerViewModel.AnalyzeSolution();
        }
    }
}
