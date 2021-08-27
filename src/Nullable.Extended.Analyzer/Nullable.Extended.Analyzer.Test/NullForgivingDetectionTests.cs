using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using VerifyCS = Nullable.Extended.Analyzer.Test.Verifiers.CSharpAnalyzerVerifier<Nullable.Extended.Analyzer.NullForgivingDetectionAnalyzer>;

namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class NullForgivingDetectionTests
    {
        private static readonly Dictionary<string, ReportDiagnostic> DiagnosticOptions = new()
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
        item{|#0:!|}.ToString();
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, DiagnosticOptions);
        }

        [TestMethod]
        public async Task IgnoreJustifiedNullForgivingOperator()
        {
            const string source =
                @"class C {
    void M(object? item)
    {
        item{|#0:!|}.ToString();
        // ! some justification text
        item{|#1:!|}.ToString();
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(0)
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, DiagnosticOptions);
        }

        [TestMethod]
        public async Task NullForgivingDetectOnNullOrDefault()
        {
            const string source =
@"class C {
    string M()
    {
        string item = null{|#0:!|};
        item = default{|#1:!|};
        item = default(string){|#2:!|};
        return item;
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId).WithLocation(0),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId).WithLocation(1),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId).WithLocation(2),
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, DiagnosticOptions);
        }

        [TestMethod]
        public async Task NullForgivingDetectInLambda()
        {
            const string source = @"
using System.Collections.Generic;
using System.Linq;

class C {
    string M()
    {
        var x = Enumerable.Range(0, 10)
            .Select(i => ((object?)i.ToString(), (object?)i.ToString()))
            .Select(item => item.Item1{|#0:!|}.ToString() + item.Item2{|#1:!|}.ToString())
            .FirstOrDefault(){|#2:!|};

        return x{|#3:!|};
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.LambdaDiagnosticId).WithLocation(0),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.LambdaDiagnosticId).WithLocation(1),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(2),
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(3),
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, DiagnosticOptions);
        }

        [TestMethod]
        public async Task IgnoreJustifiedInWholeDeclaration()
        {
            const string source = @"
using System.Collections.Generic;
using System.Linq;

class C {
    string M()
    {
        // ! Justifies the whole declaration
        var x = Enumerable.Range(0, 10)
            .Select(i => ((object?)i.ToString(), (object?)i.ToString()))
            .Select(item => item.Item1{|#0:!|}.ToString() + item.Item2{|#1:!|}.ToString())
            .FirstOrDefault(){|#2:!|};

        return x{|#3:!|};
    }
}";
            var expected = new[]
            {
                DiagnosticResult.CompilerError(NullForgivingDetectionAnalyzer.GeneralDiagnosticId).WithLocation(3),
            };

            await VerifyCS.VerifyAnalyzerAsync(source, expected, DiagnosticOptions);
        }

        [TestMethod]
        public async Task JustificationOnPropertyAfterAttribute()
        {
            const string source = @"
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

class C {
        [ReadOnly(true)]
        // ! Id is never null, always serialized
        public virtual string Id { get; set; } = null{|#0:!|};
}";

            var expected = DiagnosticResult.EmptyDiagnosticResults;

            await VerifyCS.VerifyAnalyzerAsync(source, expected, DiagnosticOptions);
        }
    }

#nullable enable
    class C1
    {
        [ReadOnly(true)]
        // ! Id is never null, always serialized
        public virtual string Id { get; set; } = null!;
    }
}
