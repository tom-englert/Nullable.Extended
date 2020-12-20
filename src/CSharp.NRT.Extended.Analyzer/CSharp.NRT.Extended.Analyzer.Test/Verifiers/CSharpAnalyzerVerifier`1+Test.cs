using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp.NRT.Extended.AnalyzerTest
{
    public static partial class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        private class Test : CSharpAnalyzerTest<TAnalyzer, MSTestVerifier>
        {
            private readonly bool _ignoreSuppressedDiagnostics;

            public Test(string testCode, bool ignoreSuppressedDiagnostics)
            {
                _ignoreSuppressedDiagnostics = ignoreSuppressedDiagnostics;
                TestCode = testCode;

                SolutionTransforms.Add((solution, projectId) =>
                {
                    var compilationOptions = solution
                        .GetProject(projectId)
                        .CompilationOptions;

                    compilationOptions = compilationOptions
                        .WithGeneralDiagnosticOption(ReportDiagnostic.Error);

                    solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);

                    return solution;
                });
            }

            protected override bool IsCompilerDiagnosticIncluded(Diagnostic diagnostic, CompilerDiagnostics compilerDiagnostics)
            {
                if (_ignoreSuppressedDiagnostics && diagnostic.IsSuppressed)
                    return false;

                return base.IsCompilerDiagnosticIncluded(diagnostic, compilerDiagnostics);
            }
        }
    }
}
