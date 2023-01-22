using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.Extension.AnalyzerFramework;
using Nullable.Shared;

using TomsToolbox.Essentials;

namespace Nullable.Extended.Extension.NullForgivingAnalyzer
{
    public class NullForgivingAnalysisResult : AnalysisResult<PostfixUnaryExpressionSyntax>, IComparable<NullForgivingAnalysisResult>
    {
        public NullForgivingAnalysisResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax node, NullForgivingContext context)
            : base(analysisContext, node, node.OperatorToken.GetLocation())
        {
            Context = context;
            Justification = node.GetJustificationText();
        }

        public string? Justification { get; }

        public bool IsJustified => !Justification.IsNullOrEmpty();

        public NullForgivingContext Context { get; set; }

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
