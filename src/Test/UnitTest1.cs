using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

using Xunit;

namespace Test
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var project = await OpenProject();
            var documents = project
                .Documents
                .Where(d => d.Folders.Count == 0); // exclude generated files in obj folder
            var compilation = await project.GetCompilationAsync(TestContext.Current.CancellationToken);

            Assert.Single(documents);

            var diagnostics = compilation!.GetDiagnostics(TestContext.Current.CancellationToken);

            var errors = diagnostics.Where(diagnostic => diagnostic is { Severity: DiagnosticSeverity.Error, IsSuppressed: false });

            Assert.Empty(errors);

            var nullables = diagnostics.Where(IsNullableDiagnostic);

            Assert.Equal(2, nullables.Count());
        }

        async Task<Project> OpenProject([CallerFilePath] string? thisFile = null)
        {
            var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["AnalyzeSample"] = "true" });
            workspace.LoadMetadataForReferencedProjects = true;

            Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(thisFile)!, @"..\Sample"));

            return await workspace.OpenProjectAsync("Sample.csproj");
        }

        private static bool IsNullableDiagnostic(Diagnostic d)
        {
            return IsNullableDiagnosticId(d.Id);
        }

        private const string FirstNullableDiagnostic = "CS8600";
        private const string LastNullableDiagnostic = "CS8900";

        private static bool IsNullableDiagnosticId(string id)
        {
            return string.Compare(id, FirstNullableDiagnostic, StringComparison.OrdinalIgnoreCase) >= 0
                   && string.Compare(id, LastNullableDiagnostic, StringComparison.OrdinalIgnoreCase) <= 0;
        }
    }
}
