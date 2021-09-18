using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    /// <summary>
    /// Contains the information about the context within which the syntax tree was analyzed.
    /// The syntax tree belongs to a document, which is contained within a project.
    /// </summary>
    public sealed class AnalysisContext
    {
        public AnalysisContext(Document document, SyntaxTree syntaxTree, SyntaxNode syntaxRoot)
        {
            Document = document;
            SyntaxTree = syntaxTree;
            SyntaxRoot = syntaxRoot;
            Text = syntaxRoot.GetText();
        }

        public Document Document { get; }

        public SyntaxTree SyntaxTree { get; }

        public SyntaxNode SyntaxRoot { get; }

        public SourceText Text { get; set; }
    }
}