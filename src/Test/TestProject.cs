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
            return workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
                .WithMetadataReferences(References)
                .AddDocument("Source.cs", source, filePath: "Source.cs");
        }
    }
}
