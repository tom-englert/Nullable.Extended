using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

using Xunit;

namespace Test
{
    /// <summary>
    /// The same file can be part of several projects, e.g. when a project is built for several target frameworks.
    /// An operator is required if it is required in at least one of them.
    /// </summary>
    public class AnalysisResultVariantsTests
    {
        // The nullable annotation of Get depends on the project, like the annotations of the framework APIs depend on the target framework.
        private const string AnnotationDependsOnProject = """
            class C
            {
            #if A
                static string? Get() => null;
            #else
                static string Get() => "";
            #endif
                int M() => Get()!.Length;
            }
            """;

        private const string CompilationErrorDependsOnProject = """
            class C
            {
            #if B
                int Broken = Undefined;
            #endif
                int M(string? s) => s!.Length;
            }
            """;

        private const string NotRequired = """
            class C
            {
                int M(string s) => s!.Length;
            }
            """;

        [Theory]
        [InlineData("A", null)]
        [InlineData(null, "A")]
        public async Task OperatorRequiredInOneVariantIsRequired(string? firstSymbol, string? secondSymbol)
        {
            var (session, _) = await AnalyzeAsync(AnnotationDependsOnProject, firstSymbol, secondSymbol);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.True(result.IsRequired);
            Assert.False(result.HasCompilationErrors);
        }

        [Fact]
        public async Task ResultIsComposedOfTheResultsOfAllDocuments()
        {
            var (session, _) = await AnalyzeAsync(AnnotationDependsOnProject, "A", null);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            var variants = result.Variants.Cast<NullForgivingDocumentResult>().ToList();

            Assert.Equal(2, variants.Count);
            Assert.Equal(1, variants.Count(variant => variant.IsRequired == true));
            Assert.Equal(1, variants.Count(variant => variant.IsRequired == false));
        }

        [Fact]
        public async Task ResultsOfUnchangedPositionsAreReused()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var workspace = new AdhocWorkspace();
            var documents = TestProject.CreateDocuments(workspace, [NotRequired, NotRequired]);

            var session = new AnalysisSession(documents, [new NullForgivingDetectionAnalyzer()], [new NullForgivingDetectionPostProcessor()], cancellationToken);
            await session.StartAsync();
            await session.Completion;

            Assert.Equal(2, session.Results.Count);
            var resultOfSecondDocument = session.Results.Single(result => result.FilePath == "Source1.cs");

            var context = await session.UpdateAsync(documents[0].WithText(SourceText.From(NotRequired + "\n")), cancellationToken);
            Assert.NotNull(context);

            Assert.Equal(2, session.Results.Count);
            Assert.Contains(resultOfSecondDocument, session.Results);
        }

        [Fact]
        public async Task OperatorNotRequiredInAnyVariantIsNotRequired()
        {
            var (session, _) = await AnalyzeAsync(NotRequired, "A", null);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.False(result.IsRequired);
        }

        [Fact]
        public async Task UnknownVariantMakesNotRequiredUnknown()
        {
            var (session, _) = await AnalyzeAsync(CompilationErrorDependsOnProject.Replace("string? s", "string s"), "B", null);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.Null(result.IsRequired);
            Assert.True(result.HasCompilationErrors);
        }

        [Fact]
        public async Task RequiredVariantWinsOverUnknownVariant()
        {
            var (session, _) = await AnalyzeAsync(CompilationErrorDependsOnProject, "B", null);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.True(result.IsRequired);
        }

        [Fact]
        public async Task ResultsOfDifferentPositionsAreNotMerged()
        {
            var (session, _) = await AnalyzeAsync("""
                class C
                {
                    int M(string? a, string b) => a!.Length + b!.Length;
                }
                """, "A", null);

            var results = session.Results.Cast<NullForgivingAnalysisResult>().OrderBy(result => result.Column).ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal([true, false], results.Select(result => result.IsRequired));
        }

        [Fact]
        public async Task UpdatingADocumentUpdatesTheMergedState()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var (session, documents) = await AnalyzeAsync(AnnotationDependsOnProject, "A", null);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.True(result.IsRequired);

            // The variant that required the operator is replaced by one that does not.
            var context = await session.UpdateAsync(documents[0].WithText(SourceText.From(NotRequired)), cancellationToken);
            Assert.NotNull(context);
            await context.Completion;

            // The other document still has the operator at a different position.
            Assert.Equal(2, session.Results.Count);
            Assert.All(session.Results.Cast<NullForgivingAnalysisResult>(), r => Assert.False(r.IsRequired));
        }

        [Fact]
        public async Task UpdatedDocumentWithoutResultsRemovesItsVariant()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var (session, documents) = await AnalyzeAsync(AnnotationDependsOnProject, "A", null);

            var requiredResult = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.True(requiredResult.IsRequired);

            // Removing the variant that requires the operator leaves the one that does not.
            await session.UpdateAsync(documents[0].WithText(SourceText.From("class C { }")), cancellationToken);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.False(result.IsRequired);
        }

        [Fact]
        public async Task ChangeOfAVariantRaisesPropertyChangedOnTheResult()
        {
            var (session, _) = await AnalyzeAsync(AnnotationDependsOnProject, "A", null);

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            var requiredVariant = Assert.Single(result.Variants.Cast<NullForgivingDocumentResult>(), variant => variant.IsRequired == true);

            var changes = new List<string?>();
            result.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

            // Simulate that the variant is evaluated again.
            requiredVariant.SetIsRequired(false);

            Assert.Contains(nameof(NullForgivingAnalysisResult.IsRequired), changes);
            Assert.False(result.IsRequired);
        }

        private static async Task<(AnalysisSession Session, IReadOnlyList<Document> Documents)> AnalyzeAsync(string source, params string?[] preprocessorSymbols)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            // Both projects contain the same file, like the target frameworks of a multi-targeted project.
            var workspace = new AdhocWorkspace();

            var documents = preprocessorSymbols
                .SelectMany(symbol => TestProject.CreateDocuments(workspace, [source], project => WithPreprocessorSymbol(project, symbol)))
                .ToList();

            var session = new AnalysisSession(documents, [new NullForgivingDetectionAnalyzer()], [new NullForgivingDetectionPostProcessor()], cancellationToken);

            await session.StartAsync();
            await session.Completion;

            return (session, documents);
        }

        private static Project WithPreprocessorSymbol(Project project, string? symbol)
        {
            var parseOptions = (CSharpParseOptions)project.ParseOptions!;

            return project.WithParseOptions(symbol == null ? parseOptions : parseOptions.WithPreprocessorSymbols(symbol));
        }
    }
}
