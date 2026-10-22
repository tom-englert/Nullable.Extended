using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

using Xunit;

namespace Test
{
    public class AnalysisSessionTests
    {
        [Fact]
        public async Task OlderUpdateFinishingLastDoesNotReplaceNewerUpdate()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var analyzer = new GatedAnalyzer("// older");

            using var workspace = new AdhocWorkspace();
            var olderDocument = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .AddDocument("Source.cs", "// older", filePath: "Source.cs");
            var newerDocument = olderDocument.WithText(SourceText.From("// newer"));

            var session = new AnalysisSession([], [analyzer], [], cancellationToken);
            await session.StartAsync();

            var olderUpdate = session.UpdateAsync(olderDocument, cancellationToken);
            await analyzer.GatedAnalysisStarted;

            var newerContext = await session.UpdateAsync(newerDocument, cancellationToken);

            analyzer.ReleaseGate();

            Assert.Null(await olderUpdate);
            Assert.NotNull(newerContext);

            var context = Assert.Single(session.DocumentContexts);
            Assert.Same(newerContext, context);
            Assert.False(context.IsCancellationRequested);
            Assert.Equal("// newer", context.SyntaxRoot.ToFullString());
        }

        [Fact]
        public async Task SequentialUpdatesReplaceTheContext()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var workspace = new AdhocWorkspace();
            var firstDocument = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .AddDocument("Source.cs", "// first", filePath: "Source.cs");
            var secondDocument = firstDocument.WithText(SourceText.From("// second"));

            var session = new AnalysisSession([], [new GatedAnalyzer(null)], [], cancellationToken);
            await session.StartAsync();

            var firstContext = await session.UpdateAsync(firstDocument, cancellationToken);
            var secondContext = await session.UpdateAsync(secondDocument, cancellationToken);

            Assert.NotNull(firstContext);
            Assert.True(firstContext.IsCancellationRequested);
            Assert.Same(secondContext, Assert.Single(session.DocumentContexts));
        }

        [Fact]
        public async Task ConcurrentUpdatesOfTheSameDocumentLeaveOnlyTheLatest()
        {
            const int count = 20;

            var cancellationToken = TestContext.Current.CancellationToken;

            using var workspace = new AdhocWorkspace();
            var document = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .AddDocument("Source.cs", "// 0", filePath: "Source.cs");

            var session = new AnalysisSession([], [new ReverseDelayAnalyzer(count)], [], cancellationToken);
            await session.StartAsync();

            // The calls are made in order, but the older ones complete later.
            var updates = Enumerable.Range(0, count)
                .Select(index => session.UpdateAsync(document.WithText(SourceText.From($"// {index}")), cancellationToken))
                .ToArray();

            var contexts = await Task.WhenAll(updates);

            Assert.All(contexts.Take(count - 1), Assert.Null);

            var latestContext = Assert.Single(session.DocumentContexts);
            Assert.Same(contexts[count - 1], latestContext);
            Assert.Equal($"// {count - 1}", latestContext.SyntaxRoot.ToFullString());
            Assert.False(latestContext.IsCancellationRequested);
        }

        [Fact]
        public async Task ResultsReportedForTheSamePositionAreMerged()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var workspace = new AdhocWorkspace();
            var document = TestProject.CreateDocument(workspace, """
                class C
                {
                    int M(string? s) => s!.Length;
                }
                """);

            var session = new AnalysisSession([document], [new NullForgivingDetectionAnalyzer(), new NullForgivingDetectionAnalyzer()], [], cancellationToken);
            await session.StartAsync();

            var context = Assert.Single(session.DocumentContexts);
            Assert.Single(context.Results);
        }

        /// <summary>
        /// Analysis of the document with the gated text does not complete until <see cref="ReleaseGate"/> is called.
        /// </summary>
        private sealed class GatedAnalyzer(string? gatedText) : ISyntaxTreeAnalyzer
        {
            private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task GatedAnalysisStarted => _started.Task;

            public void ReleaseGate() => _gate.TrySetResult(true);

            public async Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(DocumentContext documentContext, CancellationToken cancellationToken)
            {
                if (documentContext.SyntaxRoot.ToFullString() == gatedText)
                {
                    _started.TrySetResult(true);
                    await _gate.Task;
                }

                return [];
            }
        }

        /// <summary>
        /// Delays the analysis of documents with the text "// n" the longer, the smaller n is.
        /// </summary>
        private sealed class ReverseDelayAnalyzer(int count) : ISyntaxTreeAnalyzer
        {
            public async Task<IReadOnlyCollection<AnalysisResult>> AnalyzeAsync(DocumentContext documentContext, CancellationToken cancellationToken)
            {
                var index = int.Parse(documentContext.SyntaxRoot.ToFullString().Substring("// ".Length));

                await Task.Delay((count - index) * 10, cancellationToken);

                return [];
            }
        }
    }
}
