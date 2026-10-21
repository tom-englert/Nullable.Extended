using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Shared;

using TomsToolbox.Essentials;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    public class NullForgivingAnalysisResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax node, NullForgivingContext context)
        : AnalysisResult<PostfixUnaryExpressionSyntax>(analysisContext, node, node.OperatorToken.GetLocation()), IComparable<NullForgivingAnalysisResult>
    {
        public string? Justification { get; } = node.GetJustificationText();

        public bool IsJustified => !Justification.IsNullOrEmpty();

        public NullForgivingContext Context { get; set; } = context;

        public bool? IsRequired { get; set; }

        public override int CompareTo(object? other)
        {
            return CompareTo(other as NullForgivingAnalysisResult);
        }

        public int CompareTo(NullForgivingAnalysisResult? other)
        {
            if (other is null)
                return 1;

            return GetSeverity(IsRequired) - GetSeverity(other.IsRequired);
        }

        private static int GetSeverity(bool? value)
        {
            return value switch
            {
                null => 0,
                true => 1,
                false => 2
            };
        }
    }
}
