using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Test
{
    internal static class TestProject
    {
        private static readonly ImmutableArray<MetadataReference> References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();

        /// <summary>
        /// Creates a document of a project that has nullable reference types enabled.
        /// </summary>
        public static Document CreateDocument(AdhocWorkspace workspace, string source)
        {
            return CreateDocuments(workspace, [source])[0];
        }

        /// <summary>
        /// Creates the documents of a single project that has nullable reference types enabled.
        /// The documents are named Source0.cs, Source1.cs, ...
        /// </summary>
        /// <param name="workspace">The workspace to add the project to.</param>
        /// <param name="sources">The source code of the documents.</param>
        /// <param name="configureProject">Optional modifications of the project, e.g. compilation options or analyzer references.</param>
        public static IReadOnlyList<Document> CreateDocuments(AdhocWorkspace workspace, IReadOnlyList<string> sources, Func<Project, Project>? configureProject = null)
        {
            var project = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
                .WithMetadataReferences(References);

            if (configureProject != null)
            {
                project = configureProject(project);
            }

            for (var i = 0; i < sources.Count; i++)
            {
                var fileName = $"Source{i}.cs";
                project = project.AddDocument(fileName, sources[i], filePath: fileName).Project;
            }

            return project.Documents.ToList();
        }
    }
}
