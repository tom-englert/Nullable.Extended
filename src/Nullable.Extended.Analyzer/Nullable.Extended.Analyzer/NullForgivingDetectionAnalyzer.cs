using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullForgivingDetectionAnalyzer : DiagnosticAnalyzer
    {
        public const string GeneralDiagnosticId = "NX_0001";
        public const string NullOrDefaultDiagnosticId = "NX_0002";
        public const string LambdaDiagnosticId = "NX_0003";

        private const string GeneralTitle = "Find general usages of the NullForgiving operator.";
        private const string NullOrDefaultTitle = "Find usages of the NullForgiving operator on null or default expression.";
        private const string LambdaTitle = "Find usages of the NullForgiving operator inside lambda expressions.";
        private const string MessageFormat = "Instance of NullForgiving operator detected.";
        private const string Category = "nullable";
        private const string HelpLink = "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving";

        private static readonly DiagnosticDescriptor GeneralRule = new DiagnosticDescriptor(GeneralDiagnosticId, GeneralTitle, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);
        private static readonly DiagnosticDescriptor NullOrDefaultRule = new DiagnosticDescriptor(NullOrDefaultDiagnosticId, NullOrDefaultTitle, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);
        private static readonly DiagnosticDescriptor LambdaRule = new DiagnosticDescriptor(LambdaDiagnosticId, LambdaTitle, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(GeneralRule, NullOrDefaultRule, LambdaRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(OnSuppressNullableWarningExpression, SyntaxKind.SuppressNullableWarningExpression);
        }

        private void OnSuppressNullableWarningExpression(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;

            if (IsNullOrDefaultExpression(node))
            {
                context.ReportDiagnostic(Diagnostic.Create(NullOrDefaultRule, node.GetLocation()));
                return;
            }

            if (IsInsideLambdaExpression(node))
            {
                context.ReportDiagnostic(Diagnostic.Create(LambdaRule, node.GetLocation()));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(GeneralRule, node.GetLocation()));
        }

        private bool IsInsideLambdaExpression(SyntaxNode node)
        {
            return (node.SelfAndDescendants().Any(n => n is LambdaExpressionSyntax));
        }

        private static bool IsNullOrDefaultExpression(SyntaxNode node)
        {
            if (!(node is PostfixUnaryExpressionSyntax e)) 
                return false;

            switch (e.Operand)
            {
                case LiteralExpressionSyntax l:
                    switch (l.Kind())
                    {
                        case SyntaxKind.DefaultLiteralExpression:
                        case SyntaxKind.NullLiteralExpression:
                            return true;
                    }

                    break;

                case DefaultExpressionSyntax _:
                    return true;
            }

            return false;
        }
    }

    internal static class ExtensionMethods
    {
        public static IEnumerable<SyntaxNode> SelfAndDescendants(this SyntaxNode? node)
        {
            while (node != null)
            {
                yield return node;

                node = node.Parent;
            }
        }
    }
}
