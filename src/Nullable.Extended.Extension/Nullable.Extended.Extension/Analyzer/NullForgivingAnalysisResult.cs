using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullable.Extended.Extension.AnalyzerFramework;
using TomsToolbox.Essentials;

namespace Nullable.Extended.Extension.Analyzer
{
    public class NullForgivingAnalysisResult : AnalysisResult<PostfixUnaryExpressionSyntax>, INotifyPropertyChanged, IComparable<NullForgivingAnalysisResult>
    {
        const string SuppressionCommentPrefix = "// ! ";

        public NullForgivingAnalysisResult(AnalysisContext analysisContext, PostfixUnaryExpressionSyntax token, NullForgivingContext context)
            : base(analysisContext, token, token.OperatorToken.GetLocation())
        {
            Context = context;

            var declaration = token
                .AncestorsAndSelf()
                .OfType<CSharpSyntaxNode>()
                .FirstOrDefault(i => i is StatementSyntax || i.GetType().Name.EndsWith("DeclarationSyntax"));

            if (declaration == null || !declaration.HasLeadingTrivia)
                return;

            Justification = string.Join(Environment.NewLine, declaration.GetLeadingTrivia()
                .Where(t => t.Kind() == SyntaxKind.SingleLineCommentTrivia)
                .Select(t => t.ToString())
                .Reverse()
                .TakeWhile(s => s.StartsWith(SuppressionCommentPrefix))
                .Select(s => s.Substring(SuppressionCommentPrefix.Length))
                .Reverse());
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
