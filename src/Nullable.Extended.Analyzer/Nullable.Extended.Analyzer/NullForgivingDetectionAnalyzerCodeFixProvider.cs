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
        private const string CodeFixTitle = "Suppress with comment";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => NullForgivingDetectionAnalyzer.SupportedDiagnosticIds.ToImmutableArray();

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

            var codeAction = CodeAction.Create(CodeFixTitle, _ => Task.FromResult(AddPlaceholderComment(context.Document, root, target)), CodeFixTitle);

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private static Document AddPlaceholderComment(Document document, SyntaxNode root, CSharpSyntaxNode target)
        {
            return document.WithSyntaxRoot(root.ReplaceNode(target, target.SetJustification([Justification.PlaceholderText])));
        }
    }
}
