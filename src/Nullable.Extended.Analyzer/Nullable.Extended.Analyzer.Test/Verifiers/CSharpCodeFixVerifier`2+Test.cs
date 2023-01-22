using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Nullable.Extended.Analyzer.Test.Verifiers
{
    public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, MSTestVerifier>
        {
            public Test(string testCode, string? fixedCode = null, ReferenceAssemblies? referenceAssemblies = null)
            {
                TestCode = testCode;
                FixedCode = fixedCode!;
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net60;
            }

            protected override CompilationOptions CreateCompilationOptions()
            {
                var compilationOptions = (CSharpCompilationOptions)base.CreateCompilationOptions();

                return compilationOptions
                    .WithSpecificDiagnosticOptions(CSharpVerifierHelper.NullableWarnings)
                    .WithGeneralDiagnosticOption(ReportDiagnostic.Error)
                    .WithNullableContextOptions(NullableContextOptions.Enable);
            }

            protected override ParseOptions CreateParseOptions()
            {
                return new CSharpParseOptions(LanguageVersion.CSharp10, DocumentationMode.Diagnose);
            }
        }
    }
}
