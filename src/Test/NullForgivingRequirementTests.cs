using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace Test
{
    /// <summary>
    /// Pins down which null-forgiving operators are reported as required, for every kind of place an operator can appear
    /// and for the combinations of several operators that influence each other.
    /// Expected outcomes are written as one character per operator, in document order: T = required, F = not required.
    /// </summary>
    public class NullForgivingRequirementTests
    {
        // $Q$ is replaced by "?" if the operator must be required, and by nothing if it must be unnecessary.
        // $NQ$ is the opposite, for operators that are applied to a null literal, where the declared type decides.
        private static readonly (string Name, string Members)[] Contexts =
        [
            ("method body", "int M(string$Q$ s) { return s!.Length; }"),
            ("expression body", "int M(string$Q$ s) => s!.Length;"),
            ("argument", "static void Use(string v) { }\nvoid M(string$Q$ s) { Use(s!); }"),
            ("return", "string M(string$Q$ s) { return s!; }"),
            ("local declaration", "void M(string$Q$ s) { string t = s!; }"),
            ("var inference", "int M(string$Q$ s) { var t = s!; return t.Length; }"),
            ("field assignment", "string _f = \"\";\nvoid M(string$Q$ s) { _f = s!; }"),
            ("conditional expression", "int M(string$Q$ s, bool b) => b ? s!.Length : 0;"),
            ("interpolation", "string M(string$Q$ s) => $\"{s!.Length}\";"),
            ("try block", "int M(string$Q$ s) { try { return s!.Length; } finally { } }"),
            ("foreach", "int M(System.Collections.Generic.List<string$Q$> list) { var n = 0; foreach (var s in list) { n += s!.Length; } return n; }"),
            ("lambda in method", "int M(string$Q$ s) { System.Func<int> f = () => s!.Length; return f(); }"),
            ("local function", "int M(string$Q$ s) { return Local(); int Local() => s!.Length; }"),
            ("lambda in field initializer", "System.Func<string$Q$, int> F = s => s!.Length;"),
            ("property getter", "string$Q$ _s = \"\";\nint L { get { return _s!.Length; } }"),
            ("property setter", "string$Q$ _s = \"\";\nstring P { set { _ = _s!.Length; } }"),
            ("property expression body", "string$Q$ _s = \"\";\nint L => _s!.Length;"),
            ("indexer", "string$Q$ _s = \"\";\nint this[int i] => _s!.Length;"),
            ("field initializer", "static string$Q$ Get() => \"\";\nstring F = Get()!;"),
            ("static field initializer", "static string$Q$ Get() => \"\";\nstatic string F = Get()!;"),
            ("property initializer", "static string$Q$ Get() => \"\";\nstring P { get; set; } = Get()!;"),
            ("constructor body", "string _f;\nC(string$Q$ s) { _f = s!; }"),
            ("constructor initializer", "C(string$Q$ s, int n) : this(s!) { }\nC(string v) { }"),
            ("null literal in field initializer", "string$NQ$ F = null!;"),
            ("default literal in field initializer", "string$NQ$ F = default!;"),
            ("null literal in local declaration", "void M() { string$NQ$ t = null!; }"),
            ("default parameter value", "void M(string$NQ$ s = null!) { }"),
            ("async method", "async System.Threading.Tasks.Task<int> M(string$Q$ s) { await System.Threading.Tasks.Task.Yield(); return s!.Length; }"),
            ("iterator", "System.Collections.Generic.IEnumerable<int> M(string$Q$ s) { yield return s!.Length; }"),
            ("generic method", "T M<T>(T$Q$ v) where T : class => v!;"),
            ("nested record", "record R(string$Q$ S) { public int L => S!.Length; }"),
            ("nested class with primary constructor", "class P(string$Q$ s) { public int L => s!.Length; }"),
            ("nested struct", "struct S { string$Q$ _s; public int L => _s!.Length; }"),
        ];

        public static TheoryData<string, string, string> SingleOperator()
        {
            var data = new TheoryData<string, string, string>();

            foreach (var (name, members) in Contexts)
            {
                data.Add($"{name}, required", Wrap(members.Replace("$Q$", "?").Replace("$NQ$", "")), "T");
                data.Add($"{name}, unnecessary", Wrap(members.Replace("$Q$", "").Replace("$NQ$", "?")), "F");
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(SingleOperator))]
        public async Task SingleOperatorIsClassifiedByItsContext(string name, string source, string expected)
        {
            Assert.NotNull(name);

            var results = await NullForgivingAnalysis.AnalyzeAsync([source]);

            Assert.Equal(expected, NullForgivingAnalysis.Format(results));
        }

        public static TheoryData<string, string, string> MultipleOperators() => new()
        {
            { "two required", Wrap("int M(string? a, string? b) => a!.Length + b!.Length;"), "TT" },
            { "two unnecessary", Wrap("int M(string a, string b) => a!.Length + b!.Length;"), "FF" },
            { "required, unnecessary", Wrap("int M(string? a, string b) => a!.Length + b!.Length;"), "TF" },
            { "unnecessary, required", Wrap("int M(string a, string? b) => a!.Length + b!.Length;"), "FT" },
            { "required, unnecessary, required", Wrap("int M(string? a, string b, string? c) => a!.Length + b!.Length + c!.Length;"), "TFT" },
            { "same line", Wrap("int M(string? a, string? b) { return a!.Length + b!.Length; }"), "TT" },
            { "in different members", Wrap("int A(string? s) => s!.Length;\nint B(string s) => s!.Length;"), "TF" },

            // After the first operator the variable is known to be not null, so the second one is redundant.
            { "second dereference of the same variable is redundant", Wrap("void M(string? a) { a!.ToString(); a!.GetHashCode(); }"), "TF" },
            { "same variable, both unnecessary", Wrap("void M(string a) { a!.ToString(); a!.GetHashCode(); }"), "FF" },
            { "same variable, reassigned null in between", Wrap("void M(string? a) { a!.ToString(); a = null; a!.GetHashCode(); }"), "TT" },

            // Each operator is judged on its own, with all the others in place: either of the two covers the other one.
            { "var inference, each one covers the other", Wrap("int M(string? y) { var x = y!; return x!.Length; }"), "FF" },

            { "nested, both required", Wrap("static string? Bar(string v) => v;\nstatic void Foo(string v) { }\nvoid M(string? a) { Foo(Bar(a!)!); }"), "TT" },
            { "nested, outer unnecessary", Wrap("static string Bar(string v) => v;\nstatic void Foo(string v) { }\nvoid M(string? a) { Foo(Bar(a!)!); }"), "FT" },
            { "member chain, both required", Wrap("C? Next;\nstring? Name;\nint M() => Next!.Name!.Length;"), "TT" },

            { "loop, required", Wrap("int M(System.Collections.Generic.IEnumerable<string> items) { string? last = null; var n = 0; foreach (var i in items) { n += last!.Length; last = i; } return n; }"), "T" },
            { "loop, guarded by a null check", Wrap("int M(System.Collections.Generic.IEnumerable<string> items) { string? last = null; var n = 0; foreach (var i in items) { if (last != null) n += last!.Length; last = i; } return n; }"), "F" },

            { "narrowed by an early return", Wrap("int M(string? s) { if (s == null) return 0; return s!.Length; }"), "F" },
            { "narrowed in one branch only", Wrap("int M(string? s, bool b) { if (b && s != null) { return s!.Length; } return s!.Length; }"), "FT" },

            { "member initialized by an annotated method", Wrap("string? _s;\n[System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_s))] void Init() { _s = \"\"; }\nint M() { Init(); return _s!.Length; }"), "F" },
            { "member initialized by an unannotated method", Wrap("string? _s;\nvoid Init() { _s = \"\"; }\nint M() { Init(); return _s!.Length; }"), "T" },

            // Diagnostics that exist without any operator removed must not count as caused by an operator.
            { "unrelated warning in the document", Wrap("string F = null;\nint A(string? s) => s!.Length;\nint B(string s) => s!.Length;"), "TF" },
            { "warning in the same statement", Wrap("void Use(string s) { }\nvoid M(string? a, string? b) { Use(a); Use(b!); }"), "T" },

            { "warning disabled by pragma", Wrap("#pragma warning disable CS8602\nint M(string? s) => s!.Length;\n#pragma warning restore CS8602"), "F" },
            { "nullable context disabled", Wrap("#nullable disable\nint M(string s) => s!.Length;\n#nullable restore"), "F" },
            { "nullable warnings disabled", Wrap("#nullable disable warnings\nint M(string? s) => s!.Length;\n#nullable restore warnings"), "F" },
        };

        [Theory]
        [MemberData(nameof(MultipleOperators))]
        public async Task EachOperatorIsJudgedWithAllOthersInPlace(string name, string source, string expected)
        {
            Assert.NotNull(name);

            var results = await NullForgivingAnalysis.AnalyzeAsync([source]);

            Assert.Equal(expected, NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task TopLevelStatementsRequired()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["static string? Get() => \"\";\nstring? s = Get();\nSystem.Console.WriteLine(s!.Length);"],
                ConsoleApplication);

            Assert.Equal("T", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task TopLevelStatementsUnnecessary()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["static string Get() => \"\";\nstring s = Get();\nSystem.Console.WriteLine(s!.Length);"],
                ConsoleApplication);

            Assert.Equal("F", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task TopLevelStatementsAndTypeDeclarationsAreJudgedIndependently()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["static string? Get() => \"\";\nstring? s = Get();\nSystem.Console.WriteLine(s!.Length);\nstring t = \"\";\nSystem.Console.WriteLine(t!.Length);\nclass C { int M(string? s) => s!.Length; }"],
                ConsoleApplication);

            Assert.Equal("TFT", NullForgivingAnalysis.Format(results));
        }

        [Theory]
        [InlineData("string?", "T")]
        [InlineData("string", "F")]
        public async Task OperatorDependsOnAnnotationDeclaredInAnotherDocument(string returnType, string expected)
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
            [
                "class A { int M() => B.Get()!.Length; }",
                $"static class B {{ public static {returnType} Get() => \"\"; }}"
            ]);

            Assert.Equal(expected, NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task OperatorsInSeveralDocumentsAreJudgedIndependently()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
            [
                "class A { int M(string? s) => s!.Length; }",
                "class B { int M(string s) => s!.Length; }",
                "class C { int M(string? s) => s!.Length + s!.Length; }"
            ]);

            Assert.Equal("TFTF", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task ErrorsInAnotherDocumentDoNotAffectTheDetection()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
            [
                "class A { int M(string? s) => s!.Length; }",
                "class B { int M(string? s) => s!.Length + Undefined; }"
            ]);

            Assert.Equal("T?", NullForgivingAnalysis.Format(results));
            Assert.False(results[0].HasCompilationErrors);
            Assert.True(results[1].HasCompilationErrors);
        }

        [Fact]
        public async Task WarningsInAnotherDocumentDoNotAffectTheDetection()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
            [
                "class A { int M(string? s) => s!.Length; int N(string s) => s!.Length; }",
                "class B { string F = null; int M(string? s) => s.Length; }"
            ]);

            Assert.Equal("TF", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task OperatorIsRequiredIfRemovalIsNotSuppressedByASuppressor()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(["class C { int M(string? s) => s!.Length; }"]);

            Assert.Equal("T", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task OperatorIsNotRequiredIfRemovalIsSuppressedByASuppressor()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["class C { int M(string? s) => s!.Length; int N(string? s) => s!.Length; }"],
                project => project.AddAnalyzerReference(new AnalyzerImageReference([new NullDereferenceSuppressor()])));

            Assert.Equal("FF", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task SuppressorOnlyAffectsTheDiagnosticsItDeclares()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["class C { void Use(string v) { } void M(string? s) { Use(s!); } int N(string? s) => s!.Length; }"],
                project => project.AddAnalyzerReference(new AnalyzerImageReference([new NullDereferenceSuppressor()])));

            Assert.Equal("TF", NullForgivingAnalysis.Format(results));
        }

        [Fact]
        public async Task NullableWarningsReportedAsErrorsAreStillDetected()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["class C { int A(string? s) => s!.Length; int B(string s) => s!.Length; }"],
                NullableWarningsAsErrors);

            Assert.Equal("TF", NullForgivingAnalysis.Format(results));
            Assert.All(results, result => Assert.False(result.HasCompilationErrors));
        }

        [Fact]
        public async Task ExistingNullableErrorsDoNotMarkTheDocumentAsHavingCompilationErrors()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["class C { string F = null; int A(string? s) => s!.Length; int B(string s) => s!.Length; }"],
                NullableWarningsAsErrors);

            Assert.Equal("TF", NullForgivingAnalysis.Format(results));
            Assert.All(results, result => Assert.False(result.HasCompilationErrors));
        }

        [Fact]
        public async Task OperatorIsNotRequiredIfNullableAnalysisIsDisabledForTheProject()
        {
            var results = await NullForgivingAnalysis.AnalyzeAsync(
                ["class C { int M(string s) => s!.Length; }"],
                project => project.WithCompilationOptions(((CSharpCompilationOptions)project.CompilationOptions!).WithNullableContextOptions(NullableContextOptions.Disable)));

            Assert.Equal("F", NullForgivingAnalysis.Format(results));
        }

        private static string Wrap(string members)
        {
            return $"class C\n{{\n{members}\n}}\n";
        }

        private static Project ConsoleApplication(Project project)
        {
            return project.WithCompilationOptions(((CSharpCompilationOptions)project.CompilationOptions!).WithOutputKind(OutputKind.ConsoleApplication));
        }

        private static Project NullableWarningsAsErrors(Project project)
        {
            var options = (CSharpCompilationOptions)project.CompilationOptions!;

            return project.WithCompilationOptions(options.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    ["CS8602"] = ReportDiagnostic.Error,
                    ["CS8625"] = ReportDiagnostic.Error,
                    ["CS8604"] = ReportDiagnostic.Error,
                }));
        }

        // Only used as an instance of an analyzer reference, so the rules about hosting compiler extensions do not apply.
#pragma warning disable RS1036, RS1038, RS1041
        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        private sealed class NullDereferenceSuppressor : DiagnosticSuppressor
#pragma warning restore RS1036, RS1038, RS1041
        {
            private static readonly SuppressionDescriptor Descriptor = new("TEST0001", "CS8602", "Suppressed by the test");

            public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [Descriptor];

            public override void ReportSuppressions(SuppressionAnalysisContext context)
            {
                foreach (var diagnostic in context.ReportedDiagnostics)
                {
                    context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
                }
            }
        }
    }
}
