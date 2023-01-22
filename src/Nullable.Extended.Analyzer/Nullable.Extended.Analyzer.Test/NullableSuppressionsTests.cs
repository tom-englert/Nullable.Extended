using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.CodeAnalysis.Testing;

using static Nullable.Extended.Analyzer.Test.Verifiers.CSharpSuppressorVerifier<Nullable.Extended.Analyzer.NullableDiagnosticSuppressor>;

namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class NullableDiagnosticSuppressorTest
    {
        private static readonly ReferenceAssemblies NetFramework = ReferenceAssemblies.NetFramework.Net462.Default;

        [TestMethod]
        public async Task NoDiagnosticsShowUpOnEmptySource()
        {
            const string source = @"";

            await VerifyAnalyzerAsync(source, Array.Empty<DiagnosticResult>());
        }

        [TestMethod]
        public async Task Test_CS8602_Roslyn_Issue_49653()
        {
            const string source = """
            class Test
            {   
                private void Method(object? target1, object? target2)
                {
                    var x = target1?.ToString();
                    if (x == null)
                        return;

                    var y = {|#0:target1|}.ToString();
                    var z = {|#1:target2|}.ToString();
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(1),
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_CS8602_Roslyn_Issue_48354()
        {
            const string source = """
            class A
            {
                public B? B { get; set; }
            }

            class B
            {
            }

            static class C
            {
                public static void M(A? a)
                {
                    var x = a;
                    var y = a?.B;
                    if (y is null) return;
                    // if x is null, then y is definitely null due to `?.` operator.
                    // we can't reach this point if x is null.
                    {|#0:x|}.ToString();
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_CS8602_expression()
        {
            const string source = """
            class Test
            {   
                private void Method(object? target1, object? target2)
                {
                    var x = target1?.ToString();
                    if (x == null)
                        return;

                    var y = ({|#0:target2 ?? target1|}).ToString();
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_CS8604()
        {
            const string source = """
            class C
            {
                public string M1(string x)
                {
                    return x.ToString();
                }
                public void M2(string? x, string? y)
                {
                    if (string.IsNullOrEmpty(x))
                        return;

                    M1({|#0:x|});
                    M1({|#1:y|});
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
            };

            await VerifyAnalyzerAsync(source, expected);

            var expected2 = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
                DiagnosticResult.CompilerError("CS8604").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected2, NetFramework);
        }

        [TestMethod]
        public async Task Test_CS8604_multiple_args()
        {
            const string source = """
                class C
                {
                    public string M1(string x, string y, string z)
                    {
                        return x.ToString();
                    }
                    public void M2(string? x, string? y)
                    {
                        if (string.IsNullOrEmpty(x))
                            return;

                        var z = y ?? x;

                        M1({|#0:x|}, {|#1:y|}, {|#2:z|});
                    }
                }
                """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
            };

            await VerifyAnalyzerAsync(source, expected);

            var expected2 = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
                DiagnosticResult.CompilerError("CS8604").WithLocation(0).WithIsSuppressed(true),
                DiagnosticResult.CompilerError("CS8604").WithLocation(2).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected2, NetFramework);
        }

        [TestMethod]
        public async Task Test_CS8604_multiple_args_with_inline_expression()
        {
            const string source = """
            class C
            {
                public string M1(string x, string y, string z)
                {
                    return x.ToString();
                }
                public void M2(string? x, string? y)
                {
                    if (string.IsNullOrEmpty(x))
                        return;

                    M1({|#0:x|}, {|#1:y|}, {|#2:y ?? x|});
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
            };

            await VerifyAnalyzerAsync(source, expected);

            var expected2 = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
                DiagnosticResult.CompilerError("CS8604").WithLocation(0).WithIsSuppressed(true),
                DiagnosticResult.CompilerError("CS8604").WithLocation(2).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected2, NetFramework);
        }

        [TestMethod]
        public async Task Test_CS8603()
        {
            const string source = """
            class Test
            {   
                private object Method(object? target1, object? target2)
                {
                    var x = target1?.ToString();
                    if (x == null)
                        return new object();

                    return {|#0:target1|};
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8603").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_CS8603_return_expression()
        {
            const string source = """
            class Test
            {   
                private object Method(object? target1, object? target2)
                {
                    var x = target1?.ToString();
                    if (x == null)
                        return new object();

                    return {|#0:target2 ?? target1|};
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8603").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_CS8603_return_expression_in_parentheses()
        {
            const string source = """
            class Test
            {   
                private object Method(object? target1, object? target2)
                {
                    var x = target1?.ToString();
                    if (x == null)
                        return new object();

                    return ({|#0:target2 ?? target1|});
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8603").WithLocation(0).WithIsSuppressed(true)
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_ComplexMethod()
        {
            const string source = """
            namespace N
            {
                using System.Collections.Generic;
                using System.IO;
                using System.Linq;

                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    class ProjectFile
                    {
                        public string? ProjectName { get; set; }
                        public string? UniqueProjectName { get; set; }
                    }

                    private static FileInfo? FindProject(DirectoryInfo directory, string solutionFolder)
                    {
                        return null;
                    }

                    static void UpdateProjectNames(DirectoryInfo solutionFolder, IList<ProjectFile> allProjectFiles,
                        CancellationToken? cancellationToken)
                    {
                        var fileNamesByDirectory = allProjectFiles.GroupBy(file => file.ToString()).ToArray();

                        var solutionFolderLength = solutionFolder.FullName.Length + 1;

                        foreach (var directoryFiles in fileNamesByDirectory)
                        {
                            cancellationToken?.ThrowIfCancellationRequested();

                            var directoryPath = directoryFiles?.Key;

                            if (string.IsNullOrEmpty(directoryPath))
                                continue;

                            var directory = new DirectoryInfo(directoryPath);
                            var project = FindProject(directory, solutionFolder.FullName);

                            var projectName = directory.Name;
                            string? uniqueProjectName = null;

                            if (project != null)
                            {
                                projectName = Path.ChangeExtension(project.Name, null);

                                var fullProjectName = project.FullName;
                                if (fullProjectName.Length >= solutionFolderLength) // project found is in solution tree
                                {
                                    uniqueProjectName = fullProjectName.Substring(solutionFolderLength);
                                }
                            }

                            foreach (var file in {|#0:directoryFiles|})
                            {
                                file.ProjectName = projectName;
                                file.UniqueProjectName = uniqueProjectName;
                            }
                        }
                    }
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true),
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_ForEach()
        {
            const string source = """
            namespace N
            {
                using System.Collections.Generic;
                using System.Linq;

                class C
                {
                    static void M(IEnumerable<string>? directoryFiles)
                    {
                        var y = directoryFiles?.FirstOrDefault();
                        if (y == null)
                            return;

                        foreach (var file in {|#0:directoryFiles|})
                        {
                            var x = file;
                        }
                    }
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true),
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_ForEachWithExpression()
        {
            const string source = """
            namespace N
            {
                using System.Collections.Generic;
                using System.Linq;

                class C
                {
                    static void M(IEnumerable<string>? d1, IEnumerable<string>? directoryFiles)
                    {
                        var y = directoryFiles?.FirstOrDefault();
                        if (y == null)
                            return;

                        foreach (var file in ({|#0:d1 ?? directoryFiles|}))
                        {
                            var x = file;
                        }
                    }
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true),
            };

            await VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task Test_ArrayAccess()
        {
            const string source = """
            class Test
            {
                private void Method(object[,]? target1, object? target2)
                {
                    var x = target1?.ToString();
                    if (x == null)
                        return;

                    var y = {|#0:target1|}[0,1].ToString();
                }
            }
            """;

            var expected = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0).WithIsSuppressed(true),
            };

            await VerifyAnalyzerAsync(source, expected);
        }
    }
}
