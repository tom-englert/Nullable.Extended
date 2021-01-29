using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nullable.Extended.Extension.Analyzer;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    public class AnalysisResult : INotifyPropertyChanged
    {
        internal AnalysisResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax startingToken, NullForgivingContext context)
        {
            StartingToken = startingToken;
            AnalysisContext = analysisContext;
            Position = startingToken.OperatorToken.GetLocation().GetLineSpan();
            Context = context;
        }

        public NullForgivingContext Context { get; set; }

        public bool IsRequired { get; set; }

        public AnalysisContext AnalysisContext { get; }

        public string FilePath => AnalysisContext.SyntaxTree.FilePath;

        public FileLinePositionSpan Position { get; }

        public int Line => Position.StartLinePosition.Line + 1;

        public int Column => Position.StartLinePosition.Character + 1;

        public PostfixUnaryExpressionSyntax StartingToken { get; }

        public string Prefix => AnalysisContext.Text.ToString(TextSpan.FromBounds(0, StartingToken.Span.End));

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}