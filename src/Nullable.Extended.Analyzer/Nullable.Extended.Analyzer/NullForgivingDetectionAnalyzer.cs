using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullForgivingDetectionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NX_0001";

        private const string Title = "Find usages of the NullForgiving operator.";
        private const string MessageFormat = "Instance of NullForgiving operator detected.";
        private const string Category = "nullable";
        private const string HelpLink = "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving";

        private static readonly DiagnosticDescriptor rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Hidden, true, null, HelpLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(OnSuppressNullableWarningExpression, SyntaxKind.SuppressNullableWarningExpression);
        }

        private void OnSuppressNullableWarningExpression(SyntaxNodeAnalysisContext context)
        {
            context.ReportDiagnostic(Diagnostic.Create(rule, context.Node.GetLocation()));
        }
    }
}
