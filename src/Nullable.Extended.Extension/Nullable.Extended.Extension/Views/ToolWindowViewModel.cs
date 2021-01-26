using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Nullable.Extended.Extension.Analyzer;
using Nullable.Extended.Extension.AnalyzerFramework;
using TomsToolbox.Wpf;
using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport("Shell")]
    [Shared]
    internal class ToolWindowViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyzerEngine _analyzerEngine;
        private readonly VisualStudioWorkspace _workspace;
        private readonly DTE _dte;

        public ToolWindowViewModel(IAnalyzerEngine analyzerEngine, IServiceProvider serviceProvider)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            _analyzerEngine = analyzerEngine;
            _dte = (DTE)serviceProvider.GetService(typeof(DTE)) ?? throw new InvalidOperationException("Can't retrieve DTE service.");

            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));
            _workspace = componentModel.GetService<VisualStudioWorkspace>();
        }

        public bool IsAnalyzing { get; private set; }

        public ICommand ScanCommand => new DelegateCommand(CanScan, Scan);

        public ICommand OpenDocumentCommand => new DelegateCommand<AnalysisResult>(OpenDocument);

        private void OpenDocument(AnalysisResult result)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var window = _dte.ItemOperations.OpenFile(result.FilePath, Constants.vsext_vk_Code);
            var document = (TextDocument)window.Document.Object();

            var editPoint = document.CreateEditPoint();
            var resultPrefix = result.Prefix;
            var text = editPoint.GetText(resultPrefix.Length);
            if (!text.StartsWith(resultPrefix))
            {
                result.Context = NullForgivingContext.Invalid;
                return;
            }

            document.Selection.MoveTo(result.Line, result.Column);
            document.Selection.MoveTo(result.Line, result.Column + 1, true);
        }

        public ICollection<AnalysisResult> AnalysisResults { get; private set; } = ImmutableArray<AnalysisResult>.Empty;

        private bool CanScan()
        {
            return !IsAnalyzing && _workspace.CurrentSolution.GetDocumentsToAnalyze().Any();
        }

        private async void Scan()
        {
            if (IsAnalyzing)
                return;

            try
            {
                IsAnalyzing = true;

                var solution = _workspace.CurrentSolution;

                var documentsToAnalyze = solution.GetDocumentsToAnalyze().ToImmutableArray();

                var results = (await _analyzerEngine.AnalyzeAsync(documentsToAnalyze).ConfigureAwait(true)).ToImmutableArray();

                AnalysisResults = results;
            }
            catch
            {

            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
