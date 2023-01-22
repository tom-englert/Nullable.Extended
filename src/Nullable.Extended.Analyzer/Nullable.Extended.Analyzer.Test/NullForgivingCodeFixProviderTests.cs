using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Nullable.Extended.Analyzer.Test.Verifiers.CSharpCodeFixVerifier<
    Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzer,
    Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzerCodeFixProvider>;


namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class NullForgivingCodeFixProviderTests
    {
        [TestMethod]
        public async Task CommentIsAddedToBareItem()
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

            await VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task CommentIsAddedToItemThatAlreadyHasAComment()
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

            await VerifyCodeFixAsync(source, expected, fixedSource);
        }
    }
}
