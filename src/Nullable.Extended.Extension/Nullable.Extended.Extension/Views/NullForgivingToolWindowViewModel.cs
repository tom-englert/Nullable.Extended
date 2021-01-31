using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Nullable.Extended.Extension.Analyzer;
using Nullable.Extended.Extension.AnalyzerFramework;
using Throttle;
using TomsToolbox.Essentials;
using TomsToolbox.Wpf;
using TomsToolbox.Wpf.Composition.AttributedModel;
using Document = Microsoft.CodeAnalysis.Document;
using TextDocument = EnvDTE.TextDocument;

#pragma warning disable VSTHRD100 // Avoid async void methods

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport("Shell")]
    [Shared]
    internal class NullForgivingToolWindowViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyzerEngine _analyzerEngine;
        private readonly VisualStudioWorkspace _workspace;
        private readonly DTE _dte;
        private HashSet<DocumentId> _changedDocuments = new HashSet<DocumentId>();

        public NullForgivingToolWindowViewModel(IAnalyzerEngine analyzerEngine, IServiceProvider serviceProvider)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            _analyzerEngine = analyzerEngine;
            _dte = (DTE)serviceProvider.GetService(typeof(DTE)) ?? throw new InvalidOperationException("Can't retrieve DTE service.");

            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));
            _workspace = componentModel.GetService<VisualStudioWorkspace>();

            _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
        }

        public bool IsAnalyzing { get; private set; }

        public ImmutableList<NullForgivingAnalysisResult> AnalysisResults { get; private set; } = ImmutableList<NullForgivingAnalysisResult>.Empty;

        public ICommand ScanCommand => new DelegateCommand(CanScan, Scan);

        public ICommand OpenDocumentCommand => new DelegateCommand<NullForgivingAnalysisResult>(OpenDocument);

        public ICommand RemoveNotRequired => new DelegateCommand(HasNotRequiredOperators, RemoveNotRequiredOperators);

        private void Workspace_WorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
        {
            var documentId = e.DocumentId;

            if (e.Kind == WorkspaceChangeKind.DocumentChanged && documentId != null)
            {
                _changedDocuments.Add(documentId);
                AnalyzeChanges();
            }
        }

        [Throttled(typeof(TomsToolbox.Wpf.Throttle), 2000)]
        private async void AnalyzeChanges()
        {
            if (IsAnalyzing)
                return;

            try
            {
                var changedDocuments = Interlocked.Exchange(ref _changedDocuments, new HashSet<DocumentId>());

                var documents = changedDocuments
                    .Select(documentId => _workspace.CurrentSolution.GetDocument(documentId))
                    .ExceptNullItems()
                    .ToList();

                var documentsToAnalyze = documents.Where(document => document.ShouldBeAnalyzed());

                var diff = await ScanAsync(documentsToAnalyze);

                AnalysisResults = AnalysisResults
                    .RemoveAll(r => changedDocuments.Contains(r.AnalysisContext.Document.Id))
                    .AddRange(diff.Where(d => changedDocuments.Contains(d.AnalysisContext.Document.Id)));
            }
            catch
            {
                // 
            }
        }

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

        private bool CanScan()
        {
            return !IsAnalyzing && _workspace.CurrentSolution.GetDocumentsToAnalyze().Any();
        }

        private async void Scan()
        {
            if (IsAnalyzing)
                return;

            var documentsToAnalyze = _workspace.CurrentSolution.GetDocumentsToAnalyze().ToImmutableList();

            try
            {
                AnalysisResults = (await ScanAsync(documentsToAnalyze));
            }
            catch (Exception ex)
            {
                // just do nothing
            }
        }

        private async Task<ImmutableList<NullForgivingAnalysisResult>> ScanAsync(IEnumerable<Document> documentsToAnalyze)
        {
            if (IsAnalyzing)
                throw new InvalidOperationException("Already analyzing!");

            try
            {
                IsAnalyzing = true;

                return (await _analyzerEngine.AnalyzeAsync(documentsToAnalyze).ConfigureAwait(true))
                    .OfType<NullForgivingAnalysisResult>()
                    .ToImmutableList();
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
