using Microsoft.CodeAnalysis;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

using Xunit;

namespace Test
{
    internal static class NullForgivingAnalysis
    {
        /// <summary>
        /// Runs a complete analysis session over a project made of the given sources.
        /// The results are ordered by document, then by position in the document.
        /// </summary>
        public static async Task<IReadOnlyList<NullForgivingDocumentResult>> AnalyzeAsync(
            IReadOnlyList<string> sources,
            Func<Project, Project>? configureProject = null,
            ISyntaxAnalysisPostProcessor? postProcessor = null)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var workspace = new AdhocWorkspace();

            var documents = TestProject.CreateDocuments(workspace, sources, configureProject);

            var session = new AnalysisSession(documents, [new NullForgivingDetectionAnalyzer()], [postProcessor ?? new NullForgivingDetectionPostProcessor()], cancellationToken);

            await session.StartAsync();
            await session.Completion;

            return session.DocumentContexts
                .SelectMany(context => context.Results)
                .Cast<NullForgivingDocumentResult>()
                .OrderBy(result => result.Node.SyntaxTree.FilePath, StringComparer.Ordinal)
                .ThenBy(result => result.Node.SpanStart)
                .ToList();
        }

        /// <summary>
        /// Compact notation of the outcome: T = required, F = not required, ? = could not be determined.
        /// </summary>
        public static string Format(IEnumerable<NullForgivingDocumentResult> results)
        {
            return new string(results.Select(result => result.IsRequired switch { true => 'T', false => 'F', null => '?' }).ToArray());
        }
    }
}
