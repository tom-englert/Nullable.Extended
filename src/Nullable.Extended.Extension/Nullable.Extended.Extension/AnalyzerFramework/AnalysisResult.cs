using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullable.Extended.Extension.Analyzer;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    public class AnalysisResult : INotifyPropertyChanged
    {
        internal AnalysisResult(AnalysisContext analysisContext, string filePath, PostfixUnaryExpressionSyntax startingToken, NullForgivingContext context, string prefix)
        {
            StartingToken = startingToken;
            AnalysisContext = analysisContext;
            FilePath = filePath;
            Position = startingToken.OperatorToken.GetLocation().GetLineSpan();
            Context = context;
            Prefix = prefix;
        }

        public NullForgivingContext Context { get; set; }

        public bool IsRequired { get; set; }

        public AnalysisContext AnalysisContext { get; }

        public string FilePath { get; }

        public FileLinePositionSpan Position { get; }

        public int Line => Position.StartLinePosition.Line + 1;

        public int Column => Position.StartLinePosition.Character + 1;

        public PostfixUnaryExpressionSyntax StartingToken { get; }

        public string Prefix { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}