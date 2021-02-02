using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EnvDTE;
using Nullable.Extended.Extension.Analyzer;
using Nullable.Extended.Extension.AnalyzerFramework;
using TomsToolbox.Wpf;
using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport(nameof(NullForgivingToolWindow))]
    internal class NullForgivingToolWindowViewModel : INotifyPropertyChanged
    {
        private readonly DTE _dte;

        public NullForgivingToolWindowViewModel(IServiceProvider serviceProvider, AnalyzerViewModel analyzerViewModel)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            AnalyzerViewModel = analyzerViewModel;
            SetResults(analyzerViewModel.AnalysisResults);
            AnalyzerViewModel.AnalysisResultsChanged += AnalyzerViewModel_AnalysisResultsChanged;

            _dte = (DTE)serviceProvider.GetService(typeof(DTE)) ?? throw new InvalidOperationException("Can't retrieve DTE service.");
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

        public ICommand RemoveNotRequired => new DelegateCommand(HasNotRequiredOperators, RemoveNotRequiredOperators);

        private bool HasNotRequiredOperators()
        {
            return NotRequiredAnalysisResults.Any();
        }

        private void RemoveNotRequiredOperators()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var resultsToRemove = NotRequiredAnalysisResults;

            var resultsByDocument = resultsToRemove.GroupBy(result => result.AnalysisContext.Document);

            foreach (var documentResults in resultsByDocument)
            {
                var document = documentResults.Key;
                var window = _dte.ItemOperations.OpenFile(document.FilePath, Constants.vsext_vk_Code);
                var textDocument = (TextDocument)window.Document.Object();
                var selection = textDocument.Selection;

                foreach (var result in documentResults.OrderByDescending(r => r.Line).ThenByDescending(r => r.Column))
                {
                    selection.MoveTo(result.Line, result.Column);
                    selection.MoveTo(result.Line, result.Column + 1, true);
                    if (selection.Text == "!")
                    {
                        selection.Text = "";
                    }
                }
            }
        }

        private IEnumerable<NullForgivingAnalysisResult> NotRequiredAnalysisResults => AnalysisResults.Where(result => !result.IsRequired && result.Context.IsValid());

        private void OpenDocument(NullForgivingAnalysisResult result)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var window = _dte.ItemOperations.OpenFile(result.FilePath, Constants.vsext_vk_Code);
            var textDocument = (TextDocument)window.Document.Object();

            var editPoint = textDocument.CreateEditPoint();
            var resultPrefix = result.Prefix;
            var text = editPoint.GetText(resultPrefix.Length);
            if (!text.StartsWith(resultPrefix, StringComparison.Ordinal))
            {
                result.Context = NullForgivingContext.Modified;
                return;
            }

            textDocument.Selection.MoveTo(result.Line, result.Column);
            textDocument.Selection.MoveTo(result.Line, result.Column + 1, true);
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
