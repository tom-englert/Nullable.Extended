using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    /// <summary>
    /// Contains the information about the context within which the syntax tree was analyzed.
    /// The syntax tree belongs to a document, which is contained within a project.
    /// </summary>
    public sealed class DocumentContext
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        internal DocumentContext(Document document, SyntaxTree syntaxTree, SyntaxNode syntaxRoot)
        {
            Document = document;
            SyntaxTree = syntaxTree;
            SyntaxRoot = syntaxRoot;
        }

        public Document Document { get; }

        public SyntaxTree SyntaxTree { get; }

        public SyntaxNode SyntaxRoot { get; }

        /// <summary>
        /// The analysis results of this document.
        /// </summary>
        public IReadOnlyCollection<AnalysisResult> Results { get; internal set; } = [];

        /// <summary>
        /// Completes when the post processing of this document is done or has been cancelled.
        /// </summary>
        public Task Completion { get; internal set; } = Task.CompletedTask;

        public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// Cancels the pending post processing of this document, e.g. because the document needs to be re-evaluated.
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        internal CancellationToken CancellationToken => _cancellationTokenSource.Token;
    }
}
