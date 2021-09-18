using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nullable.Extended.Extension.AnalyzerFramework
{
    public abstract class AnalysisResult : IComparable, INotifyPropertyChanged
    {
        internal AnalysisResult(AnalysisContext analysisContext, SyntaxNode node, Location location)
        {
            Node = node;
            Location = location;
            AnalysisContext = analysisContext;
            Position = location.GetLineSpan();
        }

        public AnalysisContext AnalysisContext { get; }

        public bool HasCompilationErrors { get; set; }

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

        public Location Location { get; }

        public string Prefix => AnalysisContext.Text.ToString(TextSpan.FromBounds(0, Node.Span.End));

        public abstract int CompareTo(object? obj);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return Location + ": " + Node;
        }
    }

    public abstract class AnalysisResult<T> : AnalysisResult
        where T : SyntaxNode
    {
        internal AnalysisResult(AnalysisContext analysisContext, T node, Location location)
            : base(analysisContext, node, location)
        {
        }

        public new T Node => (T)base.Node;

        public abstract override int CompareTo(object? obj);
    }
}