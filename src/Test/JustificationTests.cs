using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Nullable.Shared;

using Xunit;

namespace Test
{
    public class JustificationTests
    {
        [Fact]
        public void NoCommentHasNoJustification()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                    {
                        return s!.Length;
                    }
                }
                """);

            Assert.False(node.HasJustificationText());
            Assert.Null(node.GetJustificationText());
        }

        [Fact]
        public void CommentAboveStatementIsTheJustification()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                    {
                        // ! the reason
                        return s!.Length;
                    }
                }
                """);

            Assert.True(node.HasJustificationText());
            Assert.Equal("the reason", node.GetJustificationText());
        }

        [Fact]
        public void ConsecutiveCommentLinesAreJoined()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                    {
                        // ! first
                        // ! second
                        return s!.Length;
                    }
                }
                """);

            Assert.Equal("first\r\nsecond", node.GetJustificationText());
        }

        [Fact]
        public void OnlyTheCommentLinesDirectlyAboveAreUsed()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                    {
                        // ! outdated
                        // some other comment
                        // ! current
                        return s!.Length;
                    }
                }
                """);

            Assert.Equal("current", node.GetJustificationText());
        }

        [Theory]
        [InlineData("// no marker")]
        [InlineData("//! no space")]
        [InlineData("/* ! block comment */")]
        public void OtherCommentsAreNotAJustification(string comment)
        {
            var node = FindOperator($$"""
                class C
                {
                    int M(string? s)
                    {
                        {{comment}}
                        return s!.Length;
                    }
                }
                """);

            Assert.False(node.HasJustificationText());
        }

        [Fact]
        public void CommentWithoutTextIsNoJustification()
        {
            // Written as a regular string, so the trailing blank of the comment is preserved.
            var node = FindOperator("class C { int M(string? s)\r\n{\r\n// ! \r\nreturn s!.Length;\r\n} }");

            Assert.False(node.HasJustificationText());
            Assert.Null(node.GetJustificationText());
        }

        [Fact]
        public void CommentAboveAnOuterStatementDoesNotApplyToNestedStatements()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                    {
                        // ! applies to the if only
                        if (s != null)
                        {
                            return s!.Length;
                        }

                        return 0;
                    }
                }
                """);

            Assert.False(node.HasJustificationText());
        }

        [Fact]
        public void CommentAboveDeclarationIsTheJustification()
        {
            var node = FindOperator("""
                class C
                {
                    // ! set by the serializer
                    public string Name { get; } = null!;
                }
                """);

            Assert.Equal("set by the serializer", node.GetJustificationText());
        }

        [Fact]
        public void CommentAboveExpressionBodyIsTheJustification()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                        // ! checked by the caller
                        => s!.Length;
                }
                """);

            Assert.Equal("checked by the caller", node.GetJustificationText());
        }

        [Fact]
        public void FirstCommentOfTheTargetWins()
        {
            var node = FindOperator("""
                class C
                {
                    // ! the first
                    int M(string? s)
                        // ! the second
                        => s!.Length;
                }
                """);

            Assert.Equal("the first", node.GetJustificationText());
        }

        [Fact]
        public void CommentInTheMiddleOfAnExpressionIsNotRecognized()
        {
            var node = FindOperator("""
                class C
                {
                    int M(string? s)
                    {
                        var length =
                            // ! not above the statement
                            s!.Length;

                        return length;
                    }
                }
                """);

            Assert.False(node.HasJustificationText());
        }

        [Fact]
        public void CommentBehindTheOperatorIsNotRecognized()
        {
            var node = FindOperator("class C { int M(string? s)\r\n{\r\nreturn s!\r\n// ! after the operator\r\n.Length;\r\n} }");

            Assert.False(node.HasJustificationText());
        }

        [Theory]
        [InlineData("int M(string? s) => s!.Length;", SyntaxKind.MethodDeclaration)]
        [InlineData("int M(string? s) { return s!.Length; }", SyntaxKind.ReturnStatement)]
        [InlineData("string? f = null; string g = f!;", SyntaxKind.FieldDeclaration)]
        [InlineData("void M(string? s) { string t = s!; }", SyntaxKind.LocalDeclarationStatement)]
        [InlineData("void M(string? s) { System.Func<int> f = () => { return s!.Length; }; }", SyntaxKind.ReturnStatement)]
        public void SuppressionCommentTargetIsTheClosestStatementOrDeclaration(string member, SyntaxKind expectedKind)
        {
            var node = FindOperator($"class C {{ {member} }}");

            Assert.Equal(expectedKind, node.FindSuppressionCommentTarget()?.Kind());
        }

        [Theory]
        [InlineData("\n"), InlineData("\r\n")]
        public void SetJustificationInsertsTheLinesWithIndentationAndLineEnding(string lineEnding)
        {
            const string source = """
                class C
                {
                    int M(string? s)
                    {
                        return s!.Length;
                    }
                }
                """;

            const string expected = """
                class C
                {
                    int M(string? s)
                    {
                        // ! first
                        // ! second
                        return s!.Length;
                    }
                }
                """;

            Assert.Equal(expected.ReplaceLineEndings(lineEnding), SetJustification(source.ReplaceLineEndings(lineEnding), "first", "second"));
        }

        [Fact]
        public void SetJustificationReplacesAnExistingBlock()
        {
            const string source = """
                class C
                {
                    int M(string? s)
                    {
                        // other
                        // ! TODO:
                        // ! more
                        return s!.Length;
                    }
                }
                """;

            const string expected = """
                class C
                {
                    int M(string? s)
                    {
                        // other
                        // ! the reason
                        return s!.Length;
                    }
                }
                """;

            Assert.Equal(expected, SetJustification(source, "the reason"));
        }

        [Fact]
        public void SetJustificationHandlesTabsAndTheFirstLineOfTheFile()
        {
            Assert.Equal("class C\r\n{\r\n\t// ! reason\r\n\tint M(string? s) => s!.Length;\r\n}", SetJustification("class C\r\n{\r\n\tint M(string? s) => s!.Length;\r\n}", "reason"));
            Assert.Equal("// ! reason\nvar x = y!;", SetJustification("var x = y!;", "reason").Replace("\r\n", "\n"));
        }

        [Fact]
        public void SetJustificationMakesTheOperatorJustified()
        {
            var node = FindOperator("class C { int M(string? s)\r\n{\r\nif (s == null) return 0;\r\n    else return s!.Length;\r\n} }");

            var target = node.FindSuppressionCommentTarget()!;
            var newRoot = node.SyntaxTree.GetRoot().ReplaceNode(target, target.SetJustification(["the reason", "and more"]));
            var newNode = newRoot.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>().Single(n => n.IsKind(SyntaxKind.SuppressNullableWarningExpression));

            Assert.Equal("the reason\r\nand more", newNode.GetJustificationText());
            Assert.True(newNode.FindSuppressionCommentTarget()!.HasLeadingJustification());
        }

        private static string SetJustification(string source, params string[] lines)
        {
            var node = FindOperator(source);
            var target = node.FindSuppressionCommentTarget()!;

            return node.SyntaxTree.GetRoot().ReplaceNode(target, target.SetJustification(lines)).ToFullString();
        }

        private static PostfixUnaryExpressionSyntax FindOperator(string source)
        {
            return CSharpSyntaxTree.ParseText(source)
                .GetRoot()
                .DescendantNodes()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Single(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression));
        }
    }
}
