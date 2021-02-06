using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullable.Extended.Extension.AnalyzerFramework;

namespace Nullable.Extended.Extension.Analyzer
{
    public class NullForgivingAnalysisResult : AnalysisResult, INotifyPropertyChanged
    {
        public NullForgivingAnalysisResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax token, NullForgivingContext context)
            : base(analysisContext, token, token.OperatorToken.GetLocation().GetLineSpan())
        {
            Context = context;
        }

        public NullForgivingContext Context { get; set; }

        public bool IsRequired { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
