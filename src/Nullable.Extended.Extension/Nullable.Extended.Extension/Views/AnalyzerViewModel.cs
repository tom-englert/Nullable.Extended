using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Nullable.Extended.Extension.AnalyzerFramework;
using PropertyChanged;
using Throttle;
using TomsToolbox.Essentials;

#pragma warning disable VSTHRD100 // Avoid async void methods

namespace Nullable.Extended.Extension.Views
{
    [Export, Shared]
    internal class AnalyzerViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyzerEngine _analyzerEngine;
        private readonly VisualStudioWorkspace _workspace;

        private HashSet<DocumentId> _changedDocuments = new HashSet<DocumentId>();

        public AnalyzerViewModel(IAnalyzerEngine analyzerEngine)
        {
            _analyzerEngine = analyzerEngine;
            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));

            _workspace = componentModel.GetService<VisualStudioWorkspace>();
            _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
        }

        public bool CanAnalyze => !IsAnalyzing && _workspace.CurrentSolution.GetDocumentsToAnalyze().Any();

        public bool IsAnalyzing { get; private set; }

        [OnChangedMethod(nameof(OnAnalysisResultsChanged))]
        public ImmutableList<AnalysisResult> AnalysisResults { get; private set; } = ImmutableList<AnalysisResult>.Empty;

        public async void AnalyzeSolution()
        {
            if (IsAnalyzing)
                return;

            var documentsToAnalyze = _workspace.CurrentSolution.GetDocumentsToAnalyze().ToImmutableList();

            try
            {
                AnalysisResults = await AnalyzeAsync(documentsToAnalyze);
            }
            catch
            {
                // just do nothing
            }
        }

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

                var diff = await AnalyzeAsync(documentsToAnalyze);

                AnalysisResults = AnalysisResults
                    .RemoveAll(r => changedDocuments.Contains(r.AnalysisContext.Document.Id))
                    .AddRange(diff.Where(d => changedDocuments.Contains(d.AnalysisContext.Document.Id)));
            }
            catch
            {
                // 
            }
        }

        private async Task<ImmutableList<AnalysisResult>> AnalyzeAsync(IEnumerable<Document> documentsToAnalyze)
        {
            if (IsAnalyzing)
                throw new InvalidOperationException("Already analyzing!");

            try
            {
                IsAnalyzing = true;

                return (await _analyzerEngine.AnalyzeAsync(documentsToAnalyze).ConfigureAwait(true))
                    .ToImmutableList();
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private void OnAnalysisResultsChanged()
        {
            AnalysisResultsChanged?.Invoke(this, new AnalysisResultsChangedArgs(AnalysisResults));
        }

        public event EventHandler<AnalysisResultsChangedArgs>? AnalysisResultsChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
