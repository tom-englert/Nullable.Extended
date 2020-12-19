using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = CSharp.NRT.Extended.Analyzer.Test.CSharpAnalyzerVerifier<CSharp.NRT.Extended.Analyzer.CSharpNRTExtendedAnalyzerAnalyzer>;

namespace CSharp.NRT.Extended.Analyzer.Test
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

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
