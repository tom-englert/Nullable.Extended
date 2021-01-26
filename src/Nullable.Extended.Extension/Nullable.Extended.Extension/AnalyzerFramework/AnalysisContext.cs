using Microsoft.CodeAnalysis;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    /// <summary>
    /// Contains the information about the context within which the syntax tree was analyzed.
    /// The syntax tree belongs to a document, which is contained within a project.
    /// </summary>
    public class AnalysisContext
    {
        public Document Document { get; }

        public AnalysisContext(Document document)
        {
            Document = document;
        }
    }
}