using System.ComponentModel;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    public abstract partial class AnalysisResult : IComparable, INotifyPropertyChanged
    {
        internal AnalysisResult(DocumentContext documentContext, SyntaxNode node, Location location)
        {
            Node = node;
            Location = location;
            DocumentContext = documentContext;
            Position = location.GetLineSpan();
        }

        public DocumentContext DocumentContext { get; }

        public bool HasCompilationErrors { get; set; }

        // Project.Name may have target framework as suffix, Project.AssemblyName may contain full path and/or target framework
        // => use file name
        public string ProjectName => Path.GetFileNameWithoutExtension(DocumentContext.Document.Project.FilePath ?? string.Empty);

        public string FilePath => DocumentContext.SyntaxTree.FilePath;

        public string RelativeFilePath
        {
            get
            {
                var solutionPath = Path.GetDirectoryName(DocumentContext.Document.Project.Solution.FilePath) ?? string.Empty;
                var documentPath = FilePath;
                var solutionPathLength = solutionPath.Length;

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

        public Location Location { get; }

        public abstract int CompareTo(object? obj);

        public override string ToString()
        {
            return Location + ": " + Node;
        }
    }

    public abstract class AnalysisResult<T> : AnalysisResult
        where T : SyntaxNode
    {
        internal AnalysisResult(DocumentContext documentContext, T node, Location location)
            : base(documentContext, node, location)
        {
        }

        public new T Node => (T)base.Node;

        public abstract override int CompareTo(object? obj);
    }
}
