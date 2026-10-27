using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;

using Nullable.Extended.Mcp;

using Xunit;

namespace Test
{
    public sealed class McpAnnotateTests : IDisposable
    {
        private static readonly string Source = """
            namespace A;

            class Foo
            {
                int M1(string? s)
                {
                    return s!.Length;
                }

                // ! TODO:
                int M2(string? s) => s!.Length;

                // ! the cache always sets it
                int M3(string? s) => s!.Length;

                int M4(string? a, string? b) => a!.Length + b!.Length;
            }
            """.ReplaceLineEndings("\n");

        // Positions of the operators in the source
        private const int M1Line = 7, M1Column = 17;
        private const int M2Line = 11, M2Column = 27;
        private const int M3Line = 14, M3Column = 27;
        private const int M4Line = 16, M4ColumnA = 38, M4ColumnB = 50;

        private readonly string _root = Path.Combine(Path.GetTempPath(), "NullableExtendedMcpAnnotateTests_" + Guid.NewGuid().ToString("N"));
        private readonly Annotator _annotator = new(NullLogger<Annotator>.Instance);
        private readonly SourceLocator _locator;

        public McpAnnotateTests()
        {
            Directory.CreateDirectory(_root);
            WriteFile("A/A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            _locator = new SourceLocator(_root, null);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
                // best effort
            }
        }

        [Theory]
        [InlineData("\n", false), InlineData("\r\n", false), InlineData("\r\n", true)]
        public async Task JustificationIsInsertedAndEverythingElseIsKept(string lineEnding, bool withBom)
        {
            WriteFile("A/Foo.cs", Source.ReplaceLineEndings(lineEnding), withBom);

            var result = await _annotator.AnnotateAsync(_locator, [Item(M1Line, M1Column, "the length is checked by the caller")], TestContext.Current.CancellationToken);

            var outcome = Assert.Single(result.Items);
            Assert.Equal(AnnotationOutcomes.Inserted, outcome.Outcome);
            Assert.Equal($"A/Foo.cs:{M1Line}:{M1Column}", outcome.Id);
            Assert.Equal(Status.Justified, outcome.Status);
            Assert.Equal(["A/Foo.cs"], result.ChangedFiles);

            var expected = Source
                .Replace("        return s!.Length;", "        // ! the length is checked by the caller\n        return s!.Length;")
                .ReplaceLineEndings(lineEnding);

            Assert.Equal(expected, ReadFile("A/Foo.cs"));
            Assert.Equal(withBom, File.ReadAllBytes(FullPath("A/Foo.cs")).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        }

        [Fact]
        public async Task PlaceholderIsReplaced()
        {
            WriteFile("A/Foo.cs", Source);

            var result = await _annotator.AnnotateAsync(_locator, [Item(M2Line, M2Column, "first line\nsecond line")], TestContext.Current.CancellationToken);

            Assert.Equal(AnnotationOutcomes.Replaced, Assert.Single(result.Items).Outcome);
            Assert.Equal(Source.Replace("// ! TODO:", "// ! first line\n    // ! second line"), ReadFile("A/Foo.cs"));
        }

        [Fact]
        public async Task OperatorsOfTheSameStatementShareOneJustification()
        {
            WriteFile("A/Foo.cs", Source);

            var result = await _annotator.AnnotateAsync(_locator, [Item(M4Line, M4ColumnB, "b is set"), Item(M4Line, M4ColumnA, "a is set")], TestContext.Current.CancellationToken);

            Assert.All(result.Items, outcome => Assert.Equal(AnnotationOutcomes.Inserted, outcome.Outcome));
            Assert.Equal(Source.Replace("    int M4", "    // ! a is set\n    // ! b is set\n    int M4"), ReadFile("A/Foo.cs"));
        }

        [Fact]
        public async Task ItemsAreValidatedOneByOne()
        {
            WriteFile("A/Foo.cs", Source);
            WriteFile("A/Foo.Designer.cs", Source);
            WriteFile("A/obj/Obj.cs", Source);
            WriteFile("A/Other.cs", "class Other { }");
            File.WriteAllText(FullPath("A/Text.txt"), "text");

            var items = new[]
            {
                Item(M1Line, M1Column, "ok"),
                Item(M1Line, M1Column, "twice"),
                Item(M1Line, M1Column + 1, "no operator here"),
                Item(1000, 1, "line out of range"),
                Item(M1Line, 1000, "column out of range"),
                Item(M1Line, M1Column, "wrong line", "return s.Length;"),
                Item(M3Line, M3Column, "already justified"),
                Item(M1Line, M1Column, "TODO: later"),
                Item(M1Line, M1Column, "   "),
                Item(M1Line, M1Column, "// ! with marker"),
                Item(M1Line, M1Column, "line\u2028break"),
                Item(M1Line, M1Column, "ok", file: "A/Foo.Designer.cs"),
                Item(M1Line, M1Column, "ok", file: "A/obj/Obj.cs"),
                Item(M1Line, M1Column, "ok", file: "A/Other.cs"),
                Item(M1Line, M1Column, "ok", file: "A/Text.txt"),
                Item(M1Line, M1Column, "ok", file: "A/Missing.cs"),
                Item(M1Line, M1Column, "ok", file: "../outside.cs")
            };

            var result = await _annotator.AnnotateAsync(_locator, items, TestContext.Current.CancellationToken);

            Assert.Equal(1, result.Inserted);
            Assert.Equal(items.Length - 1, result.Rejected);
            Assert.Equal(AnnotationOutcomes.Inserted, result.Items[0].Outcome);
            Assert.All(result.Items.Skip(1), outcome =>
            {
                Assert.Equal(AnnotationOutcomes.Rejected, outcome.Outcome);
                Assert.False(string.IsNullOrEmpty(outcome.Message));
            });

            Assert.Contains("There is more than one item", result.Items[1].Message);
            Assert.Contains("The line has been changed", result.Items[5].Message);
            Assert.Contains("already justified", result.Items[6].Message);
            Assert.Contains("placeholder", result.Items[7].Message);
            Assert.Contains("generated", result.Items[11].Message);
            Assert.Contains("build output", result.Items[12].Message);
            Assert.Contains("below the root", result.Items[16].Message);

            Assert.Equal(Source.Replace("        return s!.Length;", "        // ! ok\n        return s!.Length;"), ReadFile("A/Foo.cs"));
            Assert.Equal(Source, ReadFile("A/Foo.Designer.cs"));
            Assert.Equal(Source, ReadFile("A/obj/Obj.cs"));
        }

        [Fact]
        public async Task RejectedItemsDoNotChangeTheFile()
        {
            WriteFile("A/Foo.cs", Source);
            var before = File.GetLastWriteTimeUtc(FullPath("A/Foo.cs"));

            var result = await _annotator.AnnotateAsync(_locator, [Item(M3Line, M3Column, "already justified"), Item(1, 1, "nothing")], TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Rejected);
            Assert.Empty(result.ChangedFiles);
            Assert.Equal(before, File.GetLastWriteTimeUtc(FullPath("A/Foo.cs")));
        }

        [Fact]
        public async Task FilesThatAreNotUtf8AreNotWritten()
        {
            var bytes = Encoding.Latin1.GetBytes(Source.Replace("// ! TODO:", "// ä ö"));
            File.WriteAllBytes(FullPath("A/Foo.cs"), bytes);

            var result = await _annotator.AnnotateAsync(_locator, [Item(M1Line, M1Column, "reason")], TestContext.Current.CancellationToken);

            Assert.Contains("UTF-8", Assert.Single(result.Items).Message);
            Assert.Equal(bytes, File.ReadAllBytes(FullPath("A/Foo.cs")));
        }

        [Fact]
        public async Task ItemsOutsideOfTheSolutionAreRejected()
        {
            WriteFile("A/Foo.cs", Source);
            WriteFile("B/B.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            WriteFile("B/Bar.cs", Source);
            WriteFile("Test.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");

            var locator = new SourceLocator(_root, Path.Combine(_root, "Test.slnx"));

            var result = await _annotator.AnnotateAsync(locator, [Item(M1Line, M1Column, "reason"), Item(M1Line, M1Column, "reason", file: "B/Bar.cs")], TestContext.Current.CancellationToken);

            Assert.Equal(AnnotationOutcomes.Inserted, result.Items[0].Outcome);
            Assert.Contains("solution", result.Items[1].Message);
            Assert.Equal(Source, ReadFile("B/Bar.cs"));
        }

        [Fact]
        public async Task ScannerSeesTheJustification()
        {
            WriteFile("A/Foo.cs", Source);

            var scanner = new NullForgivingScanner(McpScannerTests.Engine.Value, NullLogger<NullForgivingScanner>.Instance);
            var scope = new ScopeFilter();

            var before = await scanner.ScanAsync(_locator, scope, TestContext.Current.CancellationToken);
            Assert.Equal([Status.Unjustified, Status.Placeholder, Status.Justified, Status.Unjustified, Status.Unjustified], before.Occurrences.Select(o => o.Status));

            await _annotator.AnnotateAsync(_locator, [Item(M1Line, M1Column, "reason"), Item(M2Line, M2Column, "reason 2"), Item(M4Line, M4ColumnA, "reason 3")], TestContext.Current.CancellationToken);

            var after = await scanner.ScanAsync(_locator, scope, TestContext.Current.CancellationToken);
            Assert.All(after.Occurrences, occurrence => Assert.Equal(Status.Justified, occurrence.Status));
            Assert.Equal("reason", after.Occurrences[0].Justification);
            Assert.Equal("reason 2", after.Occurrences[1].Justification);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  \r\n ")]
        [InlineData("TODO:")]
        [InlineData("todo: later")]
        [InlineData("// reason")]
        [InlineData("reason\n// more")]
        [InlineData("a\u0085b")]
        public void InvalidTextsAreRejected(string text)
        {
            Assert.Throws<McpException>(() => Annotator.NormalizeText(text));
        }

        [Fact]
        public void TextIsSplitIntoTrimmedLines()
        {
            Assert.Equal(["first", "second", "third"], Annotator.NormalizeText("  first \r\n\r\nsecond\nthird\r"));
            Assert.Equal(["The TODO: list is empty"], Annotator.NormalizeText("The TODO: list is empty"));
        }

        [Fact]
        public async Task ItemCountIsLimited()
        {
            await Assert.ThrowsAsync<McpException>(() => _annotator.AnnotateAsync(_locator, [], TestContext.Current.CancellationToken));

            var items = Enumerable.Range(1, Annotator.MaxItems + 1).Select(i => Item(i, 1, "reason")).ToArray();
            await Assert.ThrowsAsync<McpException>(() => _annotator.AnnotateAsync(_locator, items, TestContext.Current.CancellationToken));
        }

        private static AnnotationItem Item(int line, int column, string text, string? sourceLine = null, string file = "A/Foo.cs")
        {
            return new AnnotationItem(file, line, column, text, sourceLine);
        }

        private string FullPath(string relativePath) => Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private string ReadFile(string relativePath) => File.ReadAllText(FullPath(relativePath));

        private void WriteFile(string relativePath, string content, bool withBom = false)
        {
            var path = FullPath(relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(withBom));
        }
    }
}
