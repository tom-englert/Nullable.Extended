using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace CSharp.NRT.Extended.Analyzer.Test
{
    public static partial class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public static async Task VerifyAnalyzerAsync(string source, ICollection<DiagnosticResult> suppressed = null, ICollection<DiagnosticResult> permanent = null)
        {
            suppressed ??= Array.Empty<DiagnosticResult>();
            permanent ??= Array.Empty<DiagnosticResult>();

            var test1 = new Test(source, false);

            test1.ExpectedDiagnostics.AddRange(permanent);
            test1.ExpectedDiagnostics.AddRange(suppressed);
            await test1.RunAsync(CancellationToken.None);

            var test2 = new Test(source, true);

            test2.ExpectedDiagnostics.AddRange(permanent);
            await test2.RunAsync(CancellationToken.None);
        }
    }
}
