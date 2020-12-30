using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using VerifyCS = Nullable.Extended.AnalyzerTest.CSharpAnalyzerVerifier<Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzer>;

namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class NullForgivingDetectionTests
    {
        private static readonly Dictionary<string, ReportDiagnostic> _diagnosticOptions = new Dictionary<string, ReportDiagnostic>
        {
            { NullForgivingDetectionAnalyzer.DiagnosticId, ReportDiagnostic.Error }
        };

        [TestMethod]
        public async Task FindNullForgivingOperator()
        {
            const string source =
@"class C {
    void M(object? item)
    {
        {|#0:item!|}.ToString();
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.DiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, _diagnosticOptions);
        }

        [TestMethod]
        public async Task NullForgivingExcludeNullOrDefault()
        {
            string item = default(string)!;
            item = null!;

            const string source =
@"class C {
    string M()
    {
        string item = null!;
        item = default!;
        item = default(string)!;
        return item;
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.DiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(source, null, _diagnosticOptions);
        }


    }

    class C1
    {
        string M()
        {
            string item = null!;
            item = default!;
            item = default(string)!;
            return item;
        }
    }
}
