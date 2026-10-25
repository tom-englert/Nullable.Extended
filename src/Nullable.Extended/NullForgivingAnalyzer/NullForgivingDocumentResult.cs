using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Shared;

using TomsToolbox.Essentials;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    /// <summary>
    /// A null-forgiving operator found in a single document; see <see cref="NullForgivingAnalysisResult"/> for the result of all documents.
    /// </summary>
    public class NullForgivingDocumentResult : DocumentAnalysisResult<PostfixUnaryExpressionSyntax>
    {
        // Boxed, so it can be volatile; null means that it has not been determined.
        private volatile object? _isRequiredValue;

        internal NullForgivingDocumentResult(DocumentContext documentContext, PostfixUnaryExpressionSyntax node, NullForgivingContext context)
            : base(documentContext, node, node.OperatorToken.GetLocation())
        {
            Context = context;
            Justification = node.GetJustificationText();
        }

        public string? Justification { get; }

        public bool IsJustified => !Justification.IsNullOrEmpty();

        public NullForgivingContext Context { get; }

        /// <summary>
        /// Whether the operator is required in the context of this document; <c>null</c> if that could not be determined (yet).
        /// </summary>
        public bool? IsRequired => (bool?)_isRequiredValue;

        internal void SetIsRequired(bool isRequired)
        {
            _isRequiredValue = isRequired;

            NotifyChanged();
        }

        internal override AnalysisResult CreateResult(IReadOnlyList<DocumentAnalysisResult> variants)
        {
            return new NullForgivingAnalysisResult(variants.Cast<NullForgivingDocumentResult>().ToArray());
        }
    }
}
