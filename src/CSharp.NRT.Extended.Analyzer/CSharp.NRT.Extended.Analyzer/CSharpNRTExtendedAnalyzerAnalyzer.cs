using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp.NRT.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpNRTExtendedAnalyzerAnalyzer : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _supportedSuppressions =
        {
            new SuppressionDescriptor("NRTX_CS8602", "CS8602", "Dummy")
        };

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = _supportedSuppressions.ToImmutableArray();

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                var location = diagnostic.Location;
                var sourceTree = location.SourceTree;
                var sourceSpan = location.SourceSpan;

                var node = sourceTree.GetRoot(context.CancellationToken).FindNode(sourceSpan);
                if (!(node is IdentifierNameSyntax nameSyntax))
                    continue;

                if (nameSyntax.Identifier.Text == "target1")
                {
                    context.ReportSuppression(Suppression.Create(SupportedSuppressions[0], diagnostic));
                }
            }
        }
    }
}
