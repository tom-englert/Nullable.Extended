using System.Collections.Immutable;
using System.ComponentModel;
using System.Composition;
using System.Windows.Input;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;

using Nullable.Extended.AnalyzerFramework;

using PropertyChanged;

using Throttle;

using TomsToolbox.Essentials;

#pragma warning disable VSTHRD100 // Avoid async void methods

namespace Nullable.Extended.Extension.Views
{
    [Export, Shared]
    internal partial class AnalyzerViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyzerEngine _analyzerEngine;
        private readonly VisualStudioWorkspace _workspace;

        // The workspace raises its events on a background thread, while the changes are processed on the UI thread.
        private ImmutableHashSet<DocumentId> _changedDocuments = ImmutableHashSet<DocumentId>.Empty;

        private AnalysisSession? _session;
        private CancellationTokenSource? _sessionCancellation;
        private int _activeAnalyses;

        public AnalyzerViewModel(IAnalyzerEngine analyzerEngine)
        {
            _analyzerEngine = analyzerEngine;
            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));

            _workspace = componentModel.GetService<VisualStudioWorkspace>();
            _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
        }

        public bool CanAnalyze => !IsAnalyzing && _workspace.CurrentSolution.GetDocumentsToAnalyze().Any();

        [OnChangedMethod(nameof(RefreshCanAnalyze))]
        public bool IsAnalyzing { get; private set; }

        public ImmutableList<AnalysisResult> AnalysisResults { get; private set; } = ImmutableList<AnalysisResult>.Empty;

        public async void AnalyzeSolution()
        {
            // Everything must happen inside the try block, so EndAnalysis is guaranteed to balance BeginAnalysis.
            try
            {
                BeginAnalysis();

                // A new analysis replaces the previous session, so everything that is still running for it is obsolete.
                _sessionCancellation?.Cancel();

                var cancellation = _sessionCancellation = new CancellationTokenSource();

                var documentsToAnalyze = _workspace.CurrentSolution.GetDocumentsToAnalyze().ToImmutableList();

                var session = _session = _analyzerEngine.CreateSession(documentsToAnalyze, cancellation.Token);

                await session.StartAsync().ConfigureAwait(true);

                // Another solution analysis has replaced the session in the meantime.
                if (_session != session)
                    return;

                UpdateResults(session);
            }
            catch
            {
                // Either the analysis was cancelled, or something is wrong with the workspace so we cannot analyze the solution. Just ignore it.
            }
            finally
            {
                EndAnalysis();
            }
        }

        private void Workspace_WorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
        {
            if (e.Kind != WorkspaceChangeKind.DocumentChanged)
            {
                // The solution, a project or a document has been added, removed or reloaded, so there may be something to analyze now, or nothing any more.
                RefreshCanAnalyze();
                return;
            }

            var documentId = e.DocumentId;

            if (documentId == null)
                return;

            ImmutableInterlocked.Update(ref _changedDocuments, static (set, id) => set.Add(id), documentId);

            AnalyzeChanges();
        }

        [Throttled(typeof(TomsToolbox.Wpf.Throttle), 2000)]
        private async void AnalyzeChanges()
        {
            // Everything must happen inside the try block, so EndAnalysis is guaranteed to balance BeginAnalysis.
            try
            {
                BeginAnalysis();

                var changedDocuments = Interlocked.Exchange(ref _changedDocuments, ImmutableHashSet<DocumentId>.Empty);

                var documentsToUpdate = changedDocuments
                    .Select(documentId => _workspace.CurrentSolution.GetDocument(documentId))
                    .ExceptNullItems()
                    .Where(document => document.ShouldBeAnalyzed())
                    .ToArray();

                if (documentsToUpdate.Length == 0)
                    return;

                var session = _session;
                var cancellation = _sessionCancellation;

                if (session == null || cancellation == null)
                {
                    // No solution analysis yet, start with an empty session that only contains the changed documents.
                    cancellation = _sessionCancellation = new CancellationTokenSource();
                    session = _session = _analyzerEngine.CreateSession([], cancellation.Token);

                    await session.StartAsync().ConfigureAwait(true);
                }

                await Task.WhenAll(documentsToUpdate.Select(document => session.UpdateAsync(document, cancellation.Token)))
                    .ConfigureAwait(true);

                // Another solution analysis has replaced the session in the meantime.
                if (_session != session)
                    return;

                UpdateResults(session);
            }
            catch
            {
                // Either the analysis was cancelled, or something is wrong with the workspace so we cannot analyze the changes. Just ignore it.
            }
            finally
            {
                EndAnalysis();
            }
        }

        private void UpdateResults(AnalysisSession session)
        {
            AnalysisResults = session.Results.ToImmutableList();
        }

        private void BeginAnalysis()
        {
            IsAnalyzing = ++_activeAnalyses > 0;
        }

        private void EndAnalysis()
        {
            IsAnalyzing = --_activeAnalyses > 0;
        }

        // The command only re-evaluates CanAnalyze on user input, so it must be told when the state has changed by other means.
        private void RefreshCanAnalyze()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
