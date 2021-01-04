using System;
using System.Collections.Immutable;
using System.Linq;
using Nullable.Extended.Analyzer.SonarAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullableDiagnosticSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _supportedSuppressions =
        {
            new SuppressionDescriptor("NX_CS8602", "CS8602", "Suppress CS8602 when full graph walk proves safe access."),
            new SuppressionDescriptor("NX_CS8603", "CS8603", "Suppress CS8603 when full graph walk proves safe access."),
            new SuppressionDescriptor("NX_CS8604", "CS8604", "Suppress CS8604 when full graph walk proves safe access.")
        };

        public NullableDiagnosticSuppressor()
        {
            Logger.Log("NullableDiagnosticSuppressor.ctor");
        }

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = _supportedSuppressions.ToImmutableArray();

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            Logger.Log(() => $"ReportSuppressions: {context.ReportedDiagnostics.Length}={string.Join("|", context.ReportedDiagnostics)}");

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
                    var elementNodeLocation = elementNode.GetLocation();

                    var detectedDiagnostics = analysisContext.Analyze(elementNode, context, sourceTree);
                    var detected = detectedDiagnostics.FirstOrDefault(d => elementNodeLocation == d.Location);
                    if (detected == null)
                        continue;

                    if (detected.Id == NullPointerDereference.NotNullDiagnosticId)
                    {
                        var suppression = SupportedSuppressions.Single(item => item.SuppressedDiagnosticId == diagnostic.Id);
                        Logger.Log(() => $"  ReportSuppression: {diagnostic}");
                        context.ReportSuppression(Suppression.Create(suppression, diagnostic));
                    }
                }
                catch (Exception ex)
                {
                    // could not analyze the full graph, so just do not suppress anything.
                    Logger.Log($"  Error: {ex}");
                }
            }
        }
    }
}
