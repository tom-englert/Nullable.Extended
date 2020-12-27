using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = Nullable.Extended.AnalyzerTest.CSharpAnalyzerVerifier<Nullable.Extended.Analyzer.NullableDiagnosticSuppressor>;

namespace Nullable.Extended.AnalyzerTest
{
    [TestClass]
    public class NullableDiagnosticSuppressorTest
    {
        [TestMethod]
        public async Task NoDiagnosticsShowUpOnEmptySource()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestMethod2()
        {
            var test = 
@"using System;

namespace ConsoleApplication1
{
    class Test
    {   
        private void Method(object? target1, object? target2)
        {
            var x = target1?.ToString();
            if (x == null)
                return;

            var y = {|#0:target1|}.ToString();
            var z = {|#1:target2|}.ToString();
        }
    }
}";

            var permanent = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(1)
            };

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(test, suppressed, permanent);
        }

        [TestMethod]
        public async Task Roslyn_Issues_48354()
        {
            var test =
@"class A
{
    public B? B { get; set; }
}

class B
{
}

static class C
{
    public static void M(A? a)
    {
        var x = a;
        var y = a?.B;
        if (y is null) return;
        // if x is null, then y is definitely null due to `?.` operator.
        // we can't reach this point if x is null.
        {|#0:x|}.ToString();
    }
}";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(test, suppressed);
        }
    }
}
