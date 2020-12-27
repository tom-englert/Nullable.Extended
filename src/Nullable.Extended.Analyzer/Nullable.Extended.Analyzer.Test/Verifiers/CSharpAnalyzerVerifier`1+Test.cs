using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Nullable.Extended.Analyzer;

namespace Nullable.Extended.AnalyzerTest
{
    public static partial class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        private class Test : CSharpAnalyzerTest<TAnalyzer, MSTestVerifier>
        {
            private readonly bool _ignoreSuppressedDiagnostics;

            public Test(string testCode, bool ignoreSuppressedDiagnostics, IDictionary<string, ReportDiagnostic> diagnosticOptions = null)
            {
                _ignoreSuppressedDiagnostics = ignoreSuppressedDiagnostics;
                TestCode = testCode;
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck;
                diagnosticOptions ??= new Dictionary<string, ReportDiagnostic>();

                SolutionTransforms.Add((solution, projectId) =>
                {
                    var compilationOptions = (CSharpCompilationOptions)solution
                        .GetProject(projectId)
                        .CompilationOptions;

                    compilationOptions = compilationOptions
                        .WithGeneralDiagnosticOption(ReportDiagnostic.Error)
                        .WithSpecificDiagnosticOptions(diagnosticOptions)
                        .WithNullableContextOptions(NullableContextOptions.Enable);

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
