using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;

using Nullable.Extended.Mcp;

using Xunit;

namespace Test
{
    // These tests load real projects with MSBuild, so they need the .NET SDK and take a few seconds each.
    public sealed class McpRequiredTests : IAsyncDisposable
    {
        private const string Source = """
            namespace A;

            class Foo
            {
                string? Maybe() => null;

                int Required()
                {
                    string? s = Maybe();
                    return s!.Length;
                }

                int NotRequired()
                {
                    string s = "x";
                    return s!.Length;
                }
            }
            """;

        private const string Project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """;

        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

        private readonly string _root = Path.Combine(Path.GetTempPath(), "NullableExtendedMcpRequiredTests_" + Guid.NewGuid().ToString("N"));
        private readonly SourceLocator _locator;
        private readonly RequiredChecker _checker;

        public McpRequiredTests()
        {
            Directory.CreateDirectory(_root);
            WriteFile("A/A.csproj", Project);
            WriteFile("A/Foo.cs", Source);

            _locator = new SourceLocator(_root, null);
            _checker = new RequiredChecker(McpScannerTests.Engine.Value, new ServerOptions(_root, null, 2), NullLogger<RequiredChecker>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _checker.DisposeAsync();

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        [Fact]
        public async Task CheckRequired_ClassifiesOperators()
        {
            var files = await ResolveAsync("A/Foo.cs");

            var result = await _checker.CheckAsync(files, 60, TestContext.Current.CancellationToken);

            Assert.True(result.Complete, result.Hint);
            Assert.Equal(["yes", "no"], result.Items.Select(item => item.Required).ToArray());
            Assert.Equal(new RequiredCounts(1, 1, 0, 0, 0), result.Counts);
            Assert.Equal("ready", Assert.Single(result.Projects).State);

            var states = _checker.GetKnownStates(files);

            Assert.Equal(["no", "yes"], states.Values.Order().ToArray());
            Assert.All(result.Items, item => Assert.Equal(item.Required, states[item.Id]));
        }

        [Fact]
        public async Task CheckRequired_ChangedFile_InvalidatesResults()
        {
            var files = await ResolveAsync("A/Foo.cs");
            var cancellationToken = TestContext.Current.CancellationToken;

            var first = await _checker.CheckAsync(files, 60, cancellationToken);

            Assert.True(first.Complete, first.Hint);

            // The operator in Required() becomes unnecessary.
            WriteFile("A/Foo.cs", Source.Replace("string? s = Maybe();", "string? s = Maybe() ?? \"\";"));

            files = await ResolveAsync("A/Foo.cs");

            Assert.Empty(_checker.GetKnownStates(files));

            var second = await _checker.CheckAsync(files, 60, cancellationToken);

            Assert.True(second.Complete, second.Hint);
            Assert.Equal(["no", "no"], second.Items.Select(item => item.Required).ToArray());
        }

        [Fact]
        public async Task CheckRequired_WithoutWaiting_ReportsPendingAndCompletes()
        {
            var files = await ResolveAsync("A/Foo.cs");
            var cancellationToken = TestContext.Current.CancellationToken;

            var first = await _checker.CheckAsync(files, 0, cancellationToken);

            Assert.False(first.Complete);
            Assert.NotNull(first.Hint);

            var deadline = DateTime.UtcNow + Timeout;
            CheckResult result;

            do
            {
                await Task.Delay(500, cancellationToken);
                result = await _checker.CheckAsync(files, 5, cancellationToken);
            }
            while (!result.Complete && DateTime.UtcNow < deadline);

            Assert.True(result.Complete, result.Hint);
            Assert.Equal(["yes", "no"], result.Items.Select(item => item.Required).ToArray());
        }

        [Fact]
        public async Task CheckRequired_CompilationErrors_AreUnknown()
        {
            WriteFile("A/Broken.cs", "class Broken { int M(string? s) { int x = \"a\"; return s!.Length; } }");

            var files = await ResolveAsync("A/Broken.cs");

            var result = await _checker.CheckAsync(files, 60, TestContext.Current.CancellationToken);

            Assert.True(result.Complete, result.Hint);

            var item = Assert.Single(result.Items);

            Assert.Equal("unknown", item.Required);
            Assert.NotNull(item.Reason);
        }

        [Fact]
        public async Task ListAndSummary_ShowCheckedStates()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var scanner = new NullForgivingScanner(McpScannerTests.Engine.Value, NullLogger<NullForgivingScanner>.Instance);
            var workspaceProvider = new WorkspaceProvider(new ServerOptions(_root, null), NullLogger<WorkspaceProvider>.Instance);
            var tools = new NullForgivingTools(scanner, new CodeContextReader(scanner), _checker, workspaceProvider, NullLogger<NullForgivingTools>.Instance);

            var before = await tools.SummarizeAsync(_locator, new ScopeFilter(), cancellationToken);

            Assert.Null(before.Required);

            var files = await ResolveAsync("A/Foo.cs");
            var result = await _checker.CheckAsync(files, 60, cancellationToken);

            Assert.True(result.Complete, result.Hint);

            var summary = await tools.SummarizeAsync(_locator, new ScopeFilter(), cancellationToken);

            Assert.Equal(new RequiredCounts(1, 1, 0, 0, 0), summary.Required);

            var notRequired = await tools.ListAsync(_locator, new ScopeFilter(), null, null, "no", 100, 0, cancellationToken);

            Assert.Equal(16, Assert.Single(notRequired.Items).Line);
        }

        [Fact]
        public async Task CheckRequired_UnbuiltProjectReference_IsCompiledFromSource()
        {
            WriteFile("B/B.csproj", Project);
            WriteFile("B/Bar.cs", "namespace B; public class Bar { public static string? Get() => null; }");
            WriteFile("A/A.csproj", Project.Replace("</Project>", "  <ItemGroup><ProjectReference Include=\"..\\B\\B.csproj\" /></ItemGroup>\n</Project>"));
            WriteFile("A/Uses.cs", "class Uses { int M() { string? s = B.Bar.Get(); return s!.Length; } }");

            var files = await ResolveAsync("A/Uses.cs");
            var result = await _checker.CheckAsync(files, 60, TestContext.Current.CancellationToken);

            Assert.True(result.Complete, result.Hint);
            // The referenced project is loaded as a source project, so no build is needed.
            Assert.False(result.Projects.Single().BuildRecommended);
            Assert.Equal("yes", Assert.Single(result.Items).Required);
        }

        [Fact]
        public async Task CheckRequired_TooManyFiles_IsRejected()
        {
            WriteFile("A/Foo2.cs", "class Foo2 { }");
            WriteFile("A/Foo3.cs", "class Foo3 { }");

            var files = await ResolveAsync("A/Foo.cs", "A/Foo2.cs", "A/Foo3.cs");

            await Assert.ThrowsAsync<McpException>(() => _checker.CheckAsync(files, 0, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ResolveFiles_RejectsFilesOutsideOfProjectsAndRoot()
        {
            WriteFile("Loose.cs", "class Loose { }");

            var resolutions = await _locator.ResolveFilesAsync(["Loose.cs", "A/Missing.cs", "../outside.cs", "A/Foo.cs"], TestContext.Current.CancellationToken);

            Assert.Equal(["Loose.cs", "A/Missing.cs", "../outside.cs"], resolutions.Where(resolution => resolution.File == null).Select(resolution => resolution.Input).ToArray());
            Assert.All(resolutions.Where(resolution => resolution.File == null), resolution => Assert.NotNull(resolution.Problem));
            Assert.NotNull(resolutions.Single(resolution => resolution.Input == "A/Foo.cs").File);
        }

        private async Task<IReadOnlyList<SourceFile>> ResolveAsync(params string[] paths)
        {
            var resolutions = await _locator.ResolveFilesAsync(paths, TestContext.Current.CancellationToken);

            return resolutions.Select(resolution => resolution.File ?? throw new InvalidOperationException(resolution.Problem)).ToArray();
        }

        private void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(_root, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
