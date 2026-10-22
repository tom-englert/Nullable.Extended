using Microsoft.CodeAnalysis;

using Ninject;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

using TomsToolbox.Composition;
using TomsToolbox.Composition.Ninject;

using Xunit;

namespace Test
{
    public class AnalyzerEngineCompositionTests
    {
        [Fact]
        public async Task EngineIsComposedWithAllExportedAnalyzersAndPostProcessors()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            // Set up the container the same way as the extension package does.
            using var kernel = new StandardKernel();
            IExportProvider exportProvider = new ExportProvider(kernel);
            kernel.BindExports(typeof(IAnalyzerEngine).Assembly);
            kernel.Bind<IExportProvider>().ToConstant(exportProvider);

            var engine = exportProvider.GetExportedValue<IAnalyzerEngine>();

            using var workspace = new AdhocWorkspace();
            var document = TestProject.CreateDocument(workspace, """
                class C
                {
                    int M(string? s) => s!.Length;
                }
                """);

            var session = engine.CreateSession([document], cancellationToken);

            await session.StartAsync();
            await session.Completion;

            // The detection needs the analyzer, the verdict needs the post processor.
            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(Assert.Single(session.DocumentContexts).Results));
            Assert.True(result.IsRequired);
        }
    }
}
