using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Extended.Extension.AnalyzerFramework;
using Nullable.Shared;

using TomsToolbox.Essentials;

namespace Nullable.Extended.Extension.Analyzer
{
    public class NullForgivingAnalysisResult : AnalysisResult<PostfixUnaryExpressionSyntax>, INotifyPropertyChanged, IComparable<NullForgivingAnalysisResult>
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

        public bool IsRequired { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override int CompareTo(object? other)
        {
            return CompareTo(other as NullForgivingAnalysisResult);
        }

        public int CompareTo(NullForgivingAnalysisResult? other)
        {
            if (other is null)
                return 1;

            return IsRequired == other.IsRequired ? 0 : IsRequired ? 1 : -1;
        }
    }
}
