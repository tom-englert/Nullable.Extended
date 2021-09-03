using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Nullable.Extended.Extension.Analyzer;
using Nullable.Extended.Extension.AnalyzerFramework;
using TomsToolbox.Wpf;
using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport(nameof(NullForgivingToolWindow))]
    [Shared]
    internal class NullForgivingToolWindowViewModel : INotifyPropertyChanged
    {
        public NullForgivingToolWindowViewModel(IServiceProvider serviceProvider, AnalyzerViewModel analyzerViewModel)
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
                .OfType<NullForgivingAnalysisResult>()
                .ToList()
                .AsReadOnly();
        }

        public AnalyzerViewModel AnalyzerViewModel { get; }

        public IReadOnlyList<NullForgivingAnalysisResult> AnalysisResults { get; private set; } = Array.Empty<NullForgivingAnalysisResult>();

        public ICommand AnalyzeCommand => new DelegateCommand(CanAnalyze, Analyze);

        public ICommand OpenDocumentCommand => new DelegateCommand<NullForgivingAnalysisResult>(OpenDocument);

        private bool HasNotRequiredOperators()
        {
            return NotRequiredAnalysisResults.Any();
        }

        private IEnumerable<NullForgivingAnalysisResult> NotRequiredAnalysisResults => AnalysisResults.Where(result => !result.IsRequired && result.Context.IsValid());

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private static async void OpenDocument(NullForgivingAnalysisResult result)
        {
            try
            {
                var textView = (await VS.Documents.OpenAsync(result.FilePath).ConfigureAwait(true))?.TextView;
                if (textView == null)
                    return;

                var snapshot = textView.FormattedLineSource.SourceTextSnapshot;
                var line = snapshot.Lines.Skip(result.Line - 1).FirstOrDefault();
                var span = new SnapshotSpan(new VirtualSnapshotPoint(line, result.Column - 1).Position, new VirtualSnapshotPoint(line, result.Column).Position);

                textView.Selection.Select(span, false);
                textView.ViewScroller.EnsureSpanVisible(span);
            }
            catch
            {
                // Probably the document has already changed and the span is no longer valid. User can simply retry.
            }
        }

        private bool CanAnalyze()
        {
            return AnalyzerViewModel.CanAnalyze;
        }

        private void Analyze()
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
