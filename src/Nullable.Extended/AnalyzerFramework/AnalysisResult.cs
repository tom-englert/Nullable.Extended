using System.ComponentModel;

using Microsoft.CodeAnalysis;

namespace Nullable.Extended.AnalyzerFramework
{
    /// <summary>
    /// The result of an analysis for a position in a file, as seen by the consumer.
    /// A file can be part of several documents, e.g. when a project is built for several target frameworks, and the analysis of the
    /// document results at the same position can differ. This result is composed of all of them, and evaluates them as a whole.
    /// It reflects changes of the evaluation of its <see cref="Variants"/>, and is the only result type that notifies about changes.
    /// </summary>
    public abstract partial class AnalysisResult : INotifyPropertyChanged
    {
        internal AnalysisResult(IReadOnlyList<DocumentAnalysisResult> variants)
        {
            Variants = variants;
        }

        /// <summary>
        /// The results of all documents this result is composed of, at least one.
        /// </summary>
        public IReadOnlyList<DocumentAnalysisResult> Variants { get; }

        /// <summary>
        /// True if the result could not be fully evaluated in at least one of the <see cref="Variants"/>.
        /// </summary>
        public bool HasCompilationErrors => Variants.Any(variant => variant.HasCompilationErrors);

        // Project.Name may have target framework as suffix, Project.AssemblyName may contain full path and/or target framework
        // => use file name
        public string ProjectName => Path.GetFileNameWithoutExtension(Representative.DocumentContext.Document.Project.FilePath ?? string.Empty);

        public string FilePath => Representative.DocumentContext.SyntaxTree.FilePath;

        public string RelativeFilePath
        {
            get
            {
                var solutionPath = Path.GetDirectoryName(Representative.DocumentContext.Document.Project.Solution.FilePath) ?? string.Empty;
                var documentPath = FilePath;
                var solutionPathLength = solutionPath.Length;

                if (solutionPathLength > 0 && documentPath.StartsWith(solutionPath, StringComparison.OrdinalIgnoreCase))
                {
                    return documentPath.Substring(solutionPathLength);
                }

                return documentPath;
            }
        }

        public FileLinePositionSpan Position => Representative.Position;

        public int Line => Position.StartLinePosition.Line + 1;

        public int Column => Position.StartLinePosition.Character + 1;

        /// <summary>
        /// The variant that provides everything that is the same in all variants, like the position.
        /// </summary>
        private DocumentAnalysisResult Representative => Variants[0];

        /// <summary>
        /// Called when the evaluation of one of the <see cref="Variants"/> has changed, so the properties that depend on it can be notified.
        /// </summary>
        internal virtual void OnVariantChanged()
        {
            OnPropertyChanged(nameof(HasCompilationErrors));
        }

        public override string ToString()
        {
            return Representative.ToString();
        }
    }
}
