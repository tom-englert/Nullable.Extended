using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Shared;

using TomsToolbox.Essentials;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    public class NullForgivingAnalysisResult : AnalysisResult<PostfixUnaryExpressionSyntax>
    {
        internal NullForgivingAnalysisResult(DocumentContext documentContext, PostfixUnaryExpressionSyntax node, NullForgivingContext context)
            : base(documentContext, node, node.OperatorToken.GetLocation())
        {
            Context = context;
            Justification = node.GetJustificationText();
        }

        public string? Justification { get; }

        public bool IsJustified => !Justification.IsNullOrEmpty();

        public NullForgivingContext Context { get; }

        public bool? IsRequired { get; internal set; }
    }
}
