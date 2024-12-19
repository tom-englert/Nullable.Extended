using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Shared;

namespace Nullable.Extended.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullForgivingDetectionAnalyzerCodeFixProvider)), Shared]
    public class NullForgivingDetectionAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string CodeFixPlaceholderText = Justification.SuppressionCommentPrefix + "TODO:";
        private const string CodeFixTitle = "Suppress with comment";

        private static readonly SyntaxTriviaList CodeFixPlaceholderTrivia = CSharpSyntaxTree.ParseText(CodeFixPlaceholderText).GetRoot().GetLeadingTrivia();

        public sealed override ImmutableArray<string> FixableDiagnosticIds => NullForgivingDetectionAnalyzer.SupportedDiagnosticIds;

        public override FixAllProvider? GetFixAllProvider()
        {
            // we should edit each comment individually
            return null;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntax = root.FindToken(diagnosticSpan.Start).Parent as PostfixUnaryExpressionSyntax;

            var target = syntax?.FindSuppressionCommentTarget();
            if (target == null)
                return;

            var codeAction = CodeAction.Create(CodeFixTitle, token => ApplyFix(context.Document, root, target, token), CodeFixTitle);

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private async Task<Document> ApplyFix(Document document, SyntaxNode root, CSharpSyntaxNode targetNode, CancellationToken token)
        {
            var leadingTrivia = targetNode.GetLeadingTrivia();
            var trailingTrivia = targetNode.GetTrailingTrivia();
            var indent = leadingTrivia.LastOrDefault(item => item.IsKind(SyntaxKind.WhitespaceTrivia));

            IEnumerable<SyntaxTrivia> triviaList = [..leadingTrivia, ..trailingTrivia, SyntaxFactory.CarriageReturnLineFeed];

            var newline = triviaList.First(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));

            leadingTrivia = leadingTrivia.AddRange(CodeFixPlaceholderTrivia.Add(newline));

            if (indent != null)
            {
                leadingTrivia = leadingTrivia.Add(indent);
            }

            var statement = targetNode.WithLeadingTrivia(leadingTrivia);

            root = root.ReplaceNode(targetNode, statement);

            document = document.WithSyntaxRoot(root);

            return await Task.FromResult(document);
        }
    }
}
