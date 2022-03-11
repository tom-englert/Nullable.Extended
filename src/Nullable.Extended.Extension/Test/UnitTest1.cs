using System.Runtime.CompilerServices;

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
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
            var documents = project.Documents;
            var compilation = await project.GetCompilationAsync();

            Assert.Single(documents);

            var diagnostics = compilation!.GetDiagnostics();

            var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !diagnostic.IsSuppressed);

            Assert.Empty(errors);

            var nullables = diagnostics.Where(IsNullableDiagnostic);

            Assert.Equal(2, nullables.Count());
        }

        async Task<Project> OpenProject([CallerFilePath] string? thisFile = null)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
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