using System.Collections.Generic;
using System.Linq;
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
            { NullForgivingDetectionAnalyzer.GeneralDiagnosticId, ReportDiagnostic.Error },
            { NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId, ReportDiagnostic.Error },
            { NullForgivingDetectionAnalyzer.LambdaDiagnosticId, ReportDiagnostic.Error },
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
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, _diagnosticOptions);
        }

        [TestMethod]
        public async Task NullForgivingDetectOnNullOrDefault()
        {
            var item = default(string)!;
            item = null!;

            const string source =
@"class C {
    string M()
    {
        string item = {|#0:null!|};
        item = {|#1:default!|};
        item = {|#2:default(string)!|};
        return item;
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId).WithLocation(0),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId).WithLocation(1),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId).WithLocation(2),
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, _diagnosticOptions);
        }

        [TestMethod]
        public async Task NullForgivingDetectInLambda()
        {
            var item = default(string)!;
            item = null!;

            const string source = @"
using System.Collections.Generic;
using System.Linq;

class C {
    string M()
    {
        var x = Enumerable.Range(0, 10)
            .Select(i => ((object?)i.ToString(), (object?)i.ToString()))
            .Select(item => {|#0:item.Item1!|}.ToString() + {|#1:item.Item2!|}.ToString())
            .FirstOrDefault();

        return {|#2:x!|};
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.LambdaDiagnosticId).WithLocation(0),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.LambdaDiagnosticId).WithLocation(1),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(2),
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, _diagnosticOptions);
        }


    }

#nullable enable
    class C1
    {
        string? M(string? a)
        {
            if (a == null)
                return null;

            var b = a.ToString();
            if (b == null)
                return null;


            var x = Enumerable.Range(0, 10)
                .Select(i => ((object?)i.ToString(), (object?)i.ToString()))
                .Select(item => item.Item1!.ToString() + item.Item2!.ToString())
                .FirstOrDefault();

            return x!;
        }
    }
}
