using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Shared;

using CSharpExtensions = Microsoft.CodeAnalysis.CSharpExtensions;

namespace Nullable.Extended.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullForgivingDetectionAnalyzerCodeFixProvider)), Shared]
    public class NullForgivingDetectionAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string CodeFixPlaceholderText = "// ! TODO:\r\n";
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

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntax = root?.FindToken(diagnosticSpan.Start).Parent as PostfixUnaryExpressionSyntax;

            var baseStatement = syntax?.FindAncestorStatementOrDeclaration();

            var codeAction = CodeAction.Create(CodeFixTitle, token => ApplyFix(context.Document, root, baseStatement, token), CodeFixTitle);

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private async Task<Document> ApplyFix(Document document, SyntaxNode root, CSharpSyntaxNode baseStatement, CancellationToken token)
        {
            var leadingTrivia = baseStatement.GetLeadingTrivia();
            var indent = leadingTrivia.LastOrDefault(item => item.IsKind(SyntaxKind.WhitespaceTrivia));

            leadingTrivia = leadingTrivia.AddRange(CodeFixPlaceholderTrivia);

            if (indent != null)
            {
                leadingTrivia = leadingTrivia.Add(indent);
            }

            var statement = baseStatement.WithLeadingTrivia(leadingTrivia);

            root = root.ReplaceNode(baseStatement, statement);

            document = document.WithSyntaxRoot(root);

            return await Task.FromResult(document);
        }
    }
}
