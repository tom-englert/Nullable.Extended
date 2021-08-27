using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using VerifyCS = Nullable.Extended.Analyzer.Test.Verifiers.CSharpCodeFixVerifier<
    Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzer,
    Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzerCodeFixProvider>;


namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class NullForgivingCodeFixProviderTests
    {
        [TestMethod]
        public async Task Test1()
        {
            const string source = @"
class C {
    void M(object? item)
    {
        item{|#0:!|}.ToString();
    }
}";
            const string fixedSource = @"
class C {
    void M(object? item)
    {
        // ! TODO:
        item!.ToString();
    }
}";

            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task Test2()
        {
            const string source = @"
class C {
    void M(object? item)
    {
// Some other comment        
        item{|#0:!|}.ToString();
    }
}";
            const string fixedSource = @"
class C {
    void M(object? item)
    {
// Some other comment        
        // ! TODO:
        item!.ToString();
    }
}";

            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }
    }
}
