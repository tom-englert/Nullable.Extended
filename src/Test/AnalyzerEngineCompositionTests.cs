using Microsoft.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.NullForgivingAnalyzer;

using TomsToolbox.Composition;
using TomsToolbox.Composition.MicrosoftExtensions;

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
            var serviceCollection = new ServiceCollection();
            serviceCollection.BindExports(typeof(IAnalyzerEngine).Assembly);
            serviceCollection.AddSingleton<IExportProvider>(sp => new ExportProviderAdapter(sp));

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var exportProvider = serviceProvider.GetRequiredService<IExportProvider>();

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
            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.True(result.IsRequired);
        }

        [Fact]
        public async Task SessionWithoutPostProcessingOnlyAnalyzesTheSyntax()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var serviceCollection = new ServiceCollection();
            serviceCollection.BindExports(typeof(IAnalyzerEngine).Assembly);
            serviceCollection.AddSingleton<IExportProvider>(sp => new ExportProviderAdapter(sp));

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var engine = serviceProvider.GetRequiredService<IExportProvider>().GetExportedValue<IAnalyzerEngine>();

            using var workspace = new AdhocWorkspace();
            var document = TestProject.CreateDocument(workspace, """
                class C
                {
                    int M(string? s) => s!.Length;
                }
                """);

            var session = engine.CreateSession([document], false, cancellationToken);

            await session.StartAsync();
            await session.Completion;

            var result = Assert.IsType<NullForgivingAnalysisResult>(Assert.Single(session.Results));
            Assert.Null(result.IsRequired);
            Assert.False(result.HasCompilationErrors);
        }
    }

    public class ShouldBeAnalyzedTests
    {
        [Theory]
        [InlineData("Program.cs", true)]
        [InlineData("src/Models/Customer.cs", true)]
        [InlineData("Form1.Designer.cs", false)]
        [InlineData("Resources.g.cs", false)]
        [InlineData("Page.g.i.cs", false)]
        [InlineData("obj/Debug/net8.0/.NETCoreApp,Version=v8.0.AssemblyAttributes.cs", false)]
        [InlineData("obj/Debug/net8.0/MyProject.AssemblyInfo.cs", false)]
        [InlineData("obj/Debug/net8.0/Other.AssemblyInfo.cs", true)]
        [InlineData("TemporaryGeneratedFile_036C0B5B.cs", false)]
        [InlineData(null, true)]
        public void FilePathRulesApplyWithoutDocument(string? filePath, bool expected)
        {
            Assert.Equal(expected, Nullable.Extended.AnalyzerFramework.ExtensionMethods.ShouldBeAnalyzed(filePath, "MyProject"));
        }
    }
}
