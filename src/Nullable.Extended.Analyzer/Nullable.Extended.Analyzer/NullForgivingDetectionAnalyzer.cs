using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Nullable.Shared;

namespace Nullable.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullForgivingDetectionAnalyzer : DiagnosticAnalyzer
    {
        public const string GeneralDiagnosticId = "NX0001";
        public const string NullOrDefaultDiagnosticId = "NX0002";
        public const string LambdaDiagnosticId = "NX0003";

        private const string GeneralTitle = "Find general usages of the NullForgiving operator";
        private const string NullOrDefaultTitle = "Find usages of the NullForgiving operator on null or default expression";
        private const string LambdaTitle = "Find usages of the NullForgiving operator inside lambda expressions";
        private const string MessageFormat = "Instance of NullForgiving operator without justtification detected";
        private const string Category = "nullable";
        private const string HelpLink = "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving";

        private static readonly DiagnosticDescriptor GeneralRule = new(GeneralDiagnosticId, GeneralTitle, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);
        private static readonly DiagnosticDescriptor NullOrDefaultRule = new(NullOrDefaultDiagnosticId, NullOrDefaultTitle, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);
        private static readonly DiagnosticDescriptor LambdaRule = new(LambdaDiagnosticId, LambdaTitle, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(GeneralRule, NullOrDefaultRule, LambdaRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(OnSuppressNullableWarningExpression, SyntaxKind.SuppressNullableWarningExpression);
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }

        private static void OnSuppressNullableWarningExpression(SyntaxNodeAnalysisContext context)
        {
            var node = (PostfixUnaryExpressionSyntax)context.Node;
            if (node.HasJustificationText())
                return;

            context.ReportDiagnostic(Diagnostic.Create(GetDiagnosticDescriptor(node), node.GetDiagnosticLocation()));
        }

        private static DiagnosticDescriptor GetDiagnosticDescriptor(SyntaxNode node)
        {
            if (node.IsNullOrDefaultExpression())
            {
                return NullOrDefaultRule;
            }

            if (node.IsInsideLambdaExpression())
            {
                return LambdaRule;
            }

            return GeneralRule;
        }
    }
}
