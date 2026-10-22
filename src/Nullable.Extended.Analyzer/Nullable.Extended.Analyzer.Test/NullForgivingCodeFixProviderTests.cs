using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

using static Nullable.Extended.Analyzer.Test.Verifiers.CSharpCodeFixVerifier<
    Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzer,
    Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzerCodeFixProvider>;


namespace Nullable.Extended.Analyzer.Test
{
    public class NullForgivingCodeFixProviderTests
    {
        [Theory]
        [InlineData("\n"), InlineData("\r\n")]
        public async Task CommentIsAddedToBareItem(string lineEnding)
        {
            const string source = """
            class C {
                void M(object? item)
                {
                    item{|#0:!|}.ToString();
                }
            }
            """;

            const string fixedSource = """
            class C {
                void M(object? item)
                {
                    // ! TODO:
                    item!.ToString();
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerWarning(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCodeFixAsync(source.ReplaceLineEndings(lineEnding), expected, fixedSource.ReplaceLineEndings(lineEnding));
        }

        [Theory]
        [InlineData("\n"), InlineData("\r\n")]
        public async Task CommentIsAddedToItemThatAlreadyHasAComment(string lineEnding)
        {
            const string source = """
            class C {
                void M(object? item)
                {
            // Some other comment        
                    item{|#0:!|}.ToString();
                }
            }
            """;

            const string fixedSource = """
            class C {
                void M(object? item)
                {
            // Some other comment        
                    // ! TODO:
                    item!.ToString();
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerWarning(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCodeFixAsync(source.ReplaceLineEndings(lineEnding), expected, fixedSource.ReplaceLineEndings(lineEnding));
        }

        [Theory]
        [InlineData("\n"), InlineData("\r\n")]
        public async Task CommentIsAddedToInitOnlyProperty(string lineEnding)
        {
            const string source = """
            class C1
            {
                public string Id { get; init; } = null!;
            }
            """;

            const string fixedSource = """
            class C1
            {
                // ! TODO:
                public string Id { get; init; } = null!;
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerWarning(NullForgivingDetectionAnalyzer.InitDiagnosticId).WithSpan(3, 43, 3, 44)
            };

            await VerifyCodeFixAsync(source.ReplaceLineEndings(lineEnding), expected, fixedSource.ReplaceLineEndings(lineEnding));
        }

        [Fact]
        public async Task CommentIsAddedToTopLevelStatement()
        {
            const string source = """
            System.Console.WriteLine("Hello, World!".ToString(){|#0:!|});
            """;

            const string fixedSource = """
            // ! TODO:
            System.Console.WriteLine("Hello, World!".ToString()!);
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerWarning(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCodeFixAsync(source, expected, fixedSource, outputKind: OutputKind.ConsoleApplication);
        }
    }
}
