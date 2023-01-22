using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Nullable.Extended.Analyzer.Test.Verifiers
{
    internal static class CSharpVerifierHelper
    {
        /// <summary>
        /// By default, the compiler reports diagnostics for nullable reference types at
        /// <see cref="DiagnosticSeverity.Warning"/>, and the analyzer test framework defaults to only validating
        /// diagnostics at <see cref="DiagnosticSeverity.Error"/>. This map contains all compiler diagnostic IDs
        /// related to nullability mapped to <see cref="ReportDiagnostic.Error"/>, which is then used to enable all
        /// of these warnings for default validation during analyzer and code fix tests.
        /// </summary>
        internal static ImmutableDictionary<string, ReportDiagnostic> NullableWarnings { get; } = GetNullableWarningsFromCompiler();

        private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
        {
            string[] args = { "/warnaserror:nullable" };
            var commandLineArguments = CSharpCommandLineParser.Default.Parse(args, baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
            return commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;
        }

        public static AnalyzerTest<TVerifier> AddSolutionTransform<TVerifier>(this AnalyzerTest<TVerifier> test, Func<Solution, Project, Solution> transform)
            where TVerifier : IVerifier, new()
        {
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                return project == null ? solution : transform(solution, project);
            });

            return test;
        }

        public static AnalyzerTest<TVerifier> AddReference<TVerifier>(this AnalyzerTest<TVerifier> test, params Assembly[] localReferences)
            where TVerifier : IVerifier, new()
        {
            test.AddSolutionTransform((solution, project) =>
            {
                var localMetadataReferences = localReferences
                    .Distinct()
                    .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));

                solution = solution.WithProjectMetadataReferences(project.Id, project.MetadataReferences.Concat(localMetadataReferences));

                return solution;
            });

            return test;
        }

        public static AnalyzerTest<TVerifier> AddPackages<TVerifier>(this AnalyzerTest<TVerifier> test, params PackageIdentity[] packages)
            where TVerifier : IVerifier, new()
        {
            test.ReferenceAssemblies = test.ReferenceAssemblies.WithPackages(packages.ToImmutableArray());

            return test;
        }
    }
}
