using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = Nullable.Extended.AnalyzerTest.CSharpAnalyzerVerifier<Nullable.Extended.Analyzer.NullableDiagnosticSuppressor>;

namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class NullableDiagnosticSuppressorTest
    {
        [TestMethod]
        public async Task NoDiagnosticsShowUpOnEmptySource()
        {
            var test = @"";

            await VerifyCS.VerifySuppressorAsync(test);
        }

        [TestMethod]
        public async Task Test_CS8602_Roslyn_Issue_49653()
        {
            var test =
                @"class Test
{   
    private void Method(object? target1, object? target2)
    {
        var x = target1?.ToString();
        if (x == null)
            return;

        var y = {|#0:target1|}.ToString();
        var z = {|#1:target2|}.ToString();
    }
}";

            var permanent = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(1)
            };

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed, permanent);
        }

        [TestMethod]
        public async Task Test_CS8602_Roslyn_Issue_48354()
        {
            var test =
                @"class A
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
}";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed);
        }

        [TestMethod]
        public async Task Test_CS8602_expression()
        {
            var test =
                @"class Test
{   
    private void Method(object? target1, object? target2)
    {
        var x = target1?.ToString();
        if (x == null)
            return;

        var y = ({|#0:target2 ?? target1|}).ToString();
    }
}";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed);
        }

        [TestMethod]
        public async Task Test_CS8604()
        {
            var test =
                @"class C
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
}";

            var permanent = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1)
            };

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed, permanent);
        }

        [TestMethod]
        public async Task Test_CS8604_multiple_args()
        {
            var test =
                @"class C
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
}";

            var permanent = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
            };

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(0),
                DiagnosticResult.CompilerError("CS8604").WithLocation(2)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed, permanent);
        }

        [TestMethod]
        public async Task Test_CS8604_multiple_args_with_inline_expression()
        {
            var test =
                @"class C
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
}";

            var permanent = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(1),
            };

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8604").WithLocation(0),
                DiagnosticResult.CompilerError("CS8604").WithLocation(2)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed, permanent);
        }

        [TestMethod]
        public async Task Test_CS8603()
        {
            var test =
                @"class Test
{   
    private object Method(object? target1, object? target2)
    {
        var x = target1?.ToString();
        if (x == null)
            return new object();

        return {|#0:target1|};
    }
}";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8603").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed);
        }

        [TestMethod]
        public async Task Test_CS8603_return_expression()
        {
            var test =
                @"class Test
{   
    private object Method(object? target1, object? target2)
    {
        var x = target1?.ToString();
        if (x == null)
            return new object();

        return {|#0:target2 ?? target1|};
    }
}";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8603").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed);
        }

        [TestMethod]
        public async Task Test_CS8603_return_expression_in_parentheses()
        {
            var test =
                @"class Test
{   
    private object Method(object? target1, object? target2)
    {
        var x = target1?.ToString();
        if (x == null)
            return new object();

        return ({|#0:target2 ?? target1|});
    }
}";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8603").WithLocation(0)
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed);
        }

        [TestMethod]
        public async Task Test_ComplexMethod()
        {
            var test = @"
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

                    foreach (var file in directoryFiles)
                    {
                        file.ProjectName = projectName;
                        file.UniqueProjectName = uniqueProjectName;
                    }
                }
            }
        }
    }
";

            var supressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithSpan(58, 42, 58, 56),
            };

            await VerifyCS.VerifySuppressorAsync(test, supressed);
        }

        [TestMethod]
        public async Task Test_ForEach()
        {
            var test = @"
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
";

            var suppressed = new[]
            {
                DiagnosticResult.CompilerError("CS8602").WithLocation(0),
            };

            await VerifyCS.VerifySuppressorAsync(test, suppressed);
        }


    }

#nullable enable
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

                    foreach (var file in directoryFiles)
                    {
                        file.ProjectName = projectName;
                        file.UniqueProjectName = uniqueProjectName;
                    }
                }
            }
        }
    }
}
