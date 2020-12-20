using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using SonarAnalyzer.Helpers;
using SonarAnalyzer.Rules.CSharp;
using SonarAnalyzer.Rules.SymbolicExecution;

namespace CSharp.NRT.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpNrtExtendedAnalyzerAnalyzer : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _supportedSuppressions =
        {
            new SuppressionDescriptor("NRTX_CS8602", "CS8602", "Dummy")
        };

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = _supportedSuppressions.ToImmutableArray();

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var cancellationToken = context.CancellationToken;

            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                var location = diagnostic.Location;
                var sourceTree = location.SourceTree;
                if (sourceTree == null)
                    continue;

                var sourceSpan = location.SourceSpan;
                var elementNode = sourceTree.GetRoot(cancellationToken).FindNode(sourceSpan);

                var analysisContext = new SonarAnalysisContext2();
                var runner = new SymbolicExecutionRunner2(new NullPointerDereference());
                runner.Initialize(analysisContext);

                analysisContext.Analyze(elementNode, context, sourceTree);

                if (analysisContext.DetecteDiagnostics.All(d => d.Location != location))
                {
                    context.ReportSuppression(Suppression.Create(SupportedSuppressions[0], diagnostic));
                }
            }
        }
    }

    public class Test
    {
        public object? Method(object? target1, object? target2)
        {
            var x = target1?.ToString();
            //if (x == null)
            //    return null;


            var y = target1.ToString();
            var z = target2.ToString();
            // var a = x.ToString();

            return y ?? z;
        }
    }

}
