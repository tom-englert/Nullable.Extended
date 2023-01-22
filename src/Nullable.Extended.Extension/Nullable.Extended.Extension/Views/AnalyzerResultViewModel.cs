using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using Microsoft.VisualStudio.Shell;

using TomsToolbox.Wpf;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    internal abstract class AnalyzerResultViewModel<TResult> : INotifyPropertyChanged where TResult : AnalysisResult
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

        protected void SetResults(IEnumerable<AnalysisResult> results)
        {
            AnalysisResults = results
                .OfType<TResult>()
                .ToList()
                .AsReadOnly();
        }

        public AnalyzerViewModel AnalyzerViewModel { get; }

        public IReadOnlyList<TResult> AnalysisResults { get; private set; } = Array.Empty<TResult>();

        public ICommand AnalyzeCommand => new DelegateCommand(CanAnalyze, AnalyzeSolution);

        public static ICommand OpenInDocumentCommand => new DelegateCommand<AnalysisResult>(Views.ExtensionMethods.OpenInDocument);

        private bool CanAnalyze()
        {
            return AnalyzerViewModel.CanAnalyze;
        }

        private void AnalyzeSolution()
        {
            AnalyzerViewModel.AnalyzeSolution();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
