using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Nullable.Extended.Analyzer.Test.Verifiers
{
    public static partial class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public static Task VerifyAnalyzerAsync(string source, IEnumerable<DiagnosticResult> diagnostics, ReferenceAssemblies? referenceAssemblies = null)
        {
            return VerifyAnalyzerAsync(new Test(source, referenceAssemblies), diagnostics);
        }

        public static async Task VerifyAnalyzerAsync(Test test, IEnumerable<DiagnosticResult> diagnostics)
        {
            test.ExpectedDiagnostics.AddRange(diagnostics);

            await test.RunAsync(CancellationToken.None);
        }
    }

    public static partial class CSharpSuppressorVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticSuppressor, new()
    {
        public static Task VerifyAnalyzerAsync(string source, IEnumerable<DiagnosticResult> diagnostics, ReferenceAssemblies? referenceAssemblies = null)
        {
            var test = new CSharpAnalyzerVerifier<TAnalyzer>.Test(source, referenceAssemblies)
            {
                ReportSuppressedDiagnostics = true
            };

            return CSharpAnalyzerVerifier<TAnalyzer>.VerifyAnalyzerAsync(test, diagnostics);
        }

        public static Task VerifyAnalyzerAsync(CSharpAnalyzerVerifier<TAnalyzer>.Test test, IEnumerable<DiagnosticResult> diagnostics)
        {
            return CSharpAnalyzerVerifier<TAnalyzer>.VerifyAnalyzerAsync(test, diagnostics);
        }
    }
}
