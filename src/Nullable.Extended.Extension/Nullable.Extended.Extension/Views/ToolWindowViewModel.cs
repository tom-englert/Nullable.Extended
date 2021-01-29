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

namespace Nullable.Extended.Extension.Views
{
    [VisualCompositionExport("Shell")]
    [Shared]
    internal class ToolWindowViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyzerEngine _analyzerEngine;
        private readonly VisualStudioWorkspace _workspace;
        private readonly DTE _dte;
        private HashSet<DocumentId> _changedDocuments = new HashSet<DocumentId>();

        public ToolWindowViewModel(IAnalyzerEngine analyzerEngine, IServiceProvider serviceProvider)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            _analyzerEngine = analyzerEngine;
            _dte = (DTE)serviceProvider.GetService(typeof(DTE)) ?? throw new InvalidOperationException("Can't retrieve DTE service.");

            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));
            _workspace = componentModel.GetService<VisualStudioWorkspace>();

            _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
        }

        private void Workspace_WorkspaceChanged(object sender, Microsoft.CodeAnalysis.WorkspaceChangeEventArgs e)
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

                var projects = new HashSet<Microsoft.CodeAnalysis.Project>(documents.Select(document => document.Project));

                var documentsToAnalyze = projects.SelectMany(project => project.GetDocumentsToAnalyze());
                
                var diff = await ScanAsync(documentsToAnalyze).ConfigureAwait(true);

                AnalysisResults = AnalysisResults
                    .RemoveAll(r => changedDocuments.Contains(r.AnalysisContext.Document.Id))
                    .AddRange(diff.Where(d => changedDocuments.Contains(d.AnalysisContext.Document.Id)));
            }
            catch
            {
                // 
            }
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
            if (!text.StartsWith(resultPrefix, StringComparison.Ordinal))
            {
                result.Context = NullForgivingContext.Invalid;
                return;
            }

            document.Selection.MoveTo(result.Line, result.Column);
            document.Selection.MoveTo(result.Line, result.Column + 1, true);
        }

        public ImmutableList<AnalysisResult> AnalysisResults { get; private set; } = ImmutableList<AnalysisResult>.Empty;

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
                AnalysisResults = await ScanAsync(documentsToAnalyze).ConfigureAwait(true);
            }
            catch
            {
                // just do nothing
            }
        }

        private async Task<ImmutableList<AnalysisResult>> ScanAsync(IEnumerable<Document> documentsToAnalyze)
        {
            if (IsAnalyzing)
                throw new InvalidOperationException("Already analyzing!");

            try
            {
                IsAnalyzing = true;

                return (await _analyzerEngine.AnalyzeAsync(documentsToAnalyze)).ToImmutableList();
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
