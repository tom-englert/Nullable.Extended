using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    public class AnalysisResult
    {
        internal AnalysisResult(AnalysisContext analysisContext, SyntaxNode node, FileLinePositionSpan position)
        {
            Node = node;
            AnalysisContext = analysisContext;
            Position = position;
        }

        public AnalysisContext AnalysisContext { get; }

        // Project.Name may have target framework as suffix, Project.AssemblyName may contain full path and/or target framework
        // => use file name
        public string ProjectName => Path.GetFileNameWithoutExtension(AnalysisContext.Document.Project.FilePath);

        public string FilePath => AnalysisContext.SyntaxTree.FilePath;

        public string RelativeFilePath
        {
            get
            {
                var solutionPath = Path.GetDirectoryName(AnalysisContext.Document.Project.Solution.FilePath);
                var documentPath = FilePath;
                var solutionPathLength = solutionPath?.Length ?? 0;

                if (solutionPathLength > 0 && documentPath.StartsWith(solutionPath, StringComparison.OrdinalIgnoreCase))
                {
                    return documentPath.Substring(solutionPathLength);
                }

                return documentPath;
            }
        }

        public FileLinePositionSpan Position { get; }

        public int Line => Position.StartLinePosition.Line + 1;

        public int Column => Position.StartLinePosition.Character + 1;

        public SyntaxNode Node { get; }

        public string Prefix => AnalysisContext.Text.ToString(TextSpan.FromBounds(0, Node.Span.End));
    }

    public class AnalysisResult<T> : AnalysisResult
        where T : SyntaxNode
    {
        internal AnalysisResult(AnalysisContext analysisContext, T node, FileLinePositionSpan position)
            : base(analysisContext, node, position)
        {
        }

        public new T Node => (T)base.Node;
    }
}