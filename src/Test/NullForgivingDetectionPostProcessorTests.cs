using Microsoft.CodeAnalysis;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

using Xunit;

namespace Test
{
    public class NullForgivingDetectionPostProcessorTests
    {
        [Fact]
        public async Task RequiredOperatorIsDetected()
        {
            var results = await Analyze("""
                class C
                {
                    int M(string? s) => s!.Length;
                }
                """);

            Assert.True(Assert.Single(results).IsRequired);
        }

        [Fact]
        public async Task UnnecessaryOperatorIsDetected()
        {
            var results = await Analyze("""
                class C
                {
                    int M(string s) => s!.Length;
                }
                """);

            Assert.False(Assert.Single(results).IsRequired);
        }

        [Fact]
        public async Task DocumentWithCompilationErrorsIsMarkedAsHavingCompilationErrors()
        {
            var results = await Analyze("""
                class C
                {
                    int M(string? s) => s!.Length + Undefined;
                }
                """);

            var result = Assert.Single(results);
            Assert.True(result.HasCompilationErrors);
            Assert.Null(result.IsRequired);
        }

        [Fact]
        public async Task FailingPostProcessorMarksResultsAsHavingCompilationErrors()
        {
            var results = await Analyze("""
                class C
                {
                    int M(string? s) => s!.Length;
                }
                """, new ThrowingPostProcessor());

            var result = Assert.Single(results);
            Assert.True(result.HasCompilationErrors);
            Assert.Null(result.IsRequired);
        }

        [Theory]
        [InlineData("// why!")]
        [InlineData("/* why! */")]
        public async Task TrailingCommentContainingExclamationMarkDoesNotAffectDetection(string comment)
        {
            var results = await Analyze($$"""
                class C
                {
                    int M(string? s)
                    {
                        return s! {{comment}}
                            .Length;
                    }
                }
                """);

            Assert.True(Assert.Single(results).IsRequired);
        }

        private static async Task<IReadOnlyList<NullForgivingAnalysisResult>> Analyze(string source, ISyntaxAnalysisPostProcessor? postProcessor = null)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var workspace = new AdhocWorkspace();

            var document = TestProject.CreateDocument(workspace, source);

            var session = new AnalysisSession([document], [new NullForgivingDetectionAnalyzer()], [postProcessor ?? new NullForgivingDetectionPostProcessor()], cancellationToken);

            await session.StartAsync();
            await session.Completion;

            return session.DocumentContexts
                .SelectMany(context => context.Results)
                .Cast<NullForgivingAnalysisResult>()
                .ToList();
        }

        private sealed class ThrowingPostProcessor : ISyntaxAnalysisPostProcessor
        {
            public Task PostProcessAsync(DocumentContext documentContext, ProjectContext projectContext, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Post processing failed");
            }

            public bool IsSpecificDiagnostic(Diagnostic diagnostic) => false;
        }
    }
}
