using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer;
using SonarAnalyzer.Helpers;

namespace Nullable.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullForgivingDetectionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NXE_NullForgiving";

        private const string Title = "Find usages of the NullForgiving operator.";
        private const string MessageFormat = "Instance of NullForgiving operator detected.";
        private const string Category = "nullable";

        private static readonly DiagnosticDescriptor rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Hidden, true); 

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
