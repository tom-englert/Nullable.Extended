using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Nullable.Extended.Analyzer.Test.Verifiers
{
    public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, MSTestVerifier>
        {
            public Test(string testCode, string fixedCode = null)
            {
                TestCode = testCode;
                FixedCode = fixedCode;

                SolutionTransforms.Add((solution, projectId) =>
                {
                    var compilationOptions = (CSharpCompilationOptions)solution
                        .GetProject(projectId)
                        .CompilationOptions;

                    compilationOptions = compilationOptions
                        .WithGeneralDiagnosticOption(ReportDiagnostic.Error)
                        .WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullForgivingDetectionDiagnosticOptions))
                        .WithNullableContextOptions(NullableContextOptions.Enable);

                    solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);

                    return solution;
                });
            }
        }
    }
}
