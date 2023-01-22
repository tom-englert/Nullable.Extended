using System.Collections.Immutable;
using System.ComponentModel;
using System.Composition;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;

using PropertyChanged;

using Throttle;

using TomsToolbox.Essentials;

#pragma warning disable VSTHRD100 // Avoid async void methods

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    [Export, Shared]
    internal class AnalyzerViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyzerEngine _analyzerEngine;
        private readonly VisualStudioWorkspace _workspace;

        private HashSet<DocumentId> _changedDocuments = new();

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
                AnalysisResults = (await AnalyzeAsync(documentsToAnalyze)).ToImmutableList();
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
                    .ToArray();

                var documentFiles = new HashSet<string?>(documents.Select(d => d.FilePath), StringComparer.OrdinalIgnoreCase);

                var documentsToAnalyze = documents.Where(document => document.ShouldBeAnalyzed());

                var updated = await AnalyzeAsync(documentsToAnalyze);

                AnalysisResults = AnalysisResults
                    .RemoveAll(existing => documentFiles.Contains(existing.AnalysisContext.Document.FilePath))
                    .AddRange(updated.Where(d => documentFiles.Contains(d.AnalysisContext.Document.FilePath)));
            }
            catch
            {
                // 
            }
        }

        private async Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(IEnumerable<Document> documentsToAnalyze)
        {
            if (IsAnalyzing)
                throw new InvalidOperationException("Already analyzing!");

            try
            {
                IsAnalyzing = true;

                return await _analyzerEngine.AnalyzeAsync(documentsToAnalyze, CancellationToken.None).ConfigureAwait(true);
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
