using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    /// <summary>
    /// The result of analyzing a single document; it is created by an <see cref="ISyntaxTreeAnalyzer"/> and evaluated by the <see cref="ISyntaxAnalysisPostProcessor"/>s.
    /// The same file can be part of several documents, e.g. when a project is built for several target frameworks, so the results of different documents
    /// can be the same result seen in different contexts, and the evaluation can differ between them.
    /// Consumers should not use the document results directly, but the <see cref="AnalysisResult"/>s of <see cref="AnalysisSession.Results"/>, which compose them.
    /// </summary>
    public abstract class DocumentAnalysisResult
    {
        private volatile bool _hasCompilationErrors;
        private volatile AnalysisResult? _result;

        internal DocumentAnalysisResult(DocumentContext documentContext, SyntaxNode node, Location location)
        {
            Node = node;
            Location = location;
            DocumentContext = documentContext;
            Position = location.GetLineSpan();
        }

        public DocumentContext DocumentContext { get; }

        /// <summary>
        /// True if the result could not be fully evaluated in the context of this document.
        /// </summary>
        public bool HasCompilationErrors => _hasCompilationErrors;

        public FileLinePositionSpan Position { get; }

        public SyntaxNode Node { get; }

        public Location Location { get; }

        /// <summary>
        /// The result this document result is a part of; it is notified when the evaluation of this document result changes.
        /// </summary>
        internal AnalysisResult? Result
        {
            get => _result;
            set => _result = value;
        }

        internal void MarkAsHavingCompilationErrors()
        {
            _hasCompilationErrors = true;

            NotifyChanged();
        }

        internal void NotifyChanged()
        {
            Result?.NotifyVariantChanged();
        }

        /// <summary>
        /// Creates the result that is composed of the document results with the same position and type, including this one.
        /// </summary>
        internal abstract AnalysisResult CreateResult(IReadOnlyList<DocumentAnalysisResult> variants);

        public override string ToString()
        {
            return Location + ": " + Node;
        }
    }

    public abstract class DocumentAnalysisResult<T> : DocumentAnalysisResult
        where T : SyntaxNode
    {
        internal DocumentAnalysisResult(DocumentContext documentContext, T node, Location location)
            : base(documentContext, node, location)
        {
        }

        public new T Node => (T)base.Node;
    }
}
