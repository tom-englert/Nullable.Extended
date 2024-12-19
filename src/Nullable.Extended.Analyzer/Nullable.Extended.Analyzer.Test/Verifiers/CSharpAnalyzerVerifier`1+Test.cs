using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Nullable.Extended.Analyzer.Test.Verifiers
{
    public static partial class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            public Test(string source, ReferenceAssemblies? referenceAssemblies = null)
            {
                TestCode = source;
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net60;
            }

            public bool ReportSuppressedDiagnostics { get; set; }

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
                return new CSharpParseOptions(LanguageVersion.CSharp11, DocumentationMode.Diagnose);
            }

            protected override CompilationWithAnalyzers CreateCompilationWithAnalyzers(Compilation compilation, ImmutableArray<DiagnosticAnalyzer> analyzers, AnalyzerOptions options, CancellationToken cancellationToken)
            {
                return compilation.WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(options, null, true, false, ReportSuppressedDiagnostics));
            }
        }
    }

    public static partial class CSharpSuppressorVerifier<TAnalyzer>
    {
        public class Test : CSharpAnalyzerVerifier<TAnalyzer>.Test
        {
            public Test(string source, ReferenceAssemblies? referenceAssemblies = null) 
                : base(source, referenceAssemblies)
            {
                ReportSuppressedDiagnostics = true;
            }
        }
    }
}
