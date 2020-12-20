using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = CSharp.NRT.Extended.AnalyzerTest.CSharpAnalyzerVerifier<CSharp.NRT.Extended.Analyzer.CSharpNrtExtendedAnalyzerAnalyzer>;

namespace CSharp.NRT.Extended.AnalyzerTest
{
    [TestClass]
    public class CSharpNRTExtendedAnalyzerUnitTest
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
            var test = @"
    #nullable enable

    using System;

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
    }
}
