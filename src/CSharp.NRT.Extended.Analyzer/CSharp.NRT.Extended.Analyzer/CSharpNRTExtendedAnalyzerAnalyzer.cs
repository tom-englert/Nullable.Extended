using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using CSharp.NRT.Extended.Analyzer.SonarAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using SonarAnalyzer.Rules.CSharp;

namespace CSharp.NRT.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpNrtExtendedAnalyzerAnalyzer : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _supportedSuppressions =
        {
            new SuppressionDescriptor("NRTX_CS8602", "CS8602", "Suppress CS8602 when full graph walk proves safe access.")
        };

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = _supportedSuppressions.ToImmutableArray();

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var cancellationToken = context.CancellationToken;

            var analysisContext = new SonarAnalysisContext();
            var runner = new SymbolicExecutionRunner(new NullPointerDereference());
            runner.Initialize(analysisContext);

            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                try
                {
                    var location = diagnostic.Location;
                    var sourceTree = location.SourceTree;
                    if (sourceTree == null)
                        continue;

                    var root = sourceTree.GetRoot(cancellationToken);

                    var sourceSpan = location.SourceSpan;
                    var elementNode = root.FindNode(sourceSpan);
                    if (!(elementNode is IdentifierNameSyntax))
                        continue;

                    if (!(elementNode.Parent is MemberAccessExpressionSyntax))
                        continue;

                    var detectedDiagnostics = analysisContext.Analyze(elementNode, context, sourceTree);

                    var detected = detectedDiagnostics.FirstOrDefault(d => elementNode.Contains(root.FindNode(d.Location.SourceSpan)));

                    if (detected == null)
                        continue;

                    if (detected.Id == NullPointerDereference.NotNullDiagnosticId)
                    {
                        context.ReportSuppression(Suppression.Create(SupportedSuppressions[0], diagnostic));
                    }
                }
                catch (Exception ex)
                {
                    // could not analyze the full graph, so just do not suppress anything.
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
