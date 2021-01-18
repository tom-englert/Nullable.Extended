using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nullable.Extended.Analyzer.SonarAdapter;
using AnalysisContext = Nullable.Extended.Analyzer.SonarAdapter.AnalysisContext;

namespace Nullable.Extended.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullableDiagnosticSuppressor : DiagnosticSuppressor
    {
        private static readonly string[] SupportedSuppressionIds = { "CS8602", "CS8603", "CS8604" };

        private static SuppressionDescriptor ToSuppressionDescriptor(string id) =>
            new SuppressionDescriptor("NX_" + id, id, $"Suppress {id} when full graph walk proves safe access.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = SupportedSuppressionIds.Select(ToSuppressionDescriptor).ToImmutableArray();

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var options = Options.Read(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            
            if (options.DisableSuppressions)
                return;

            var logger = Logger.Get(options.LogFile);

            logger.Log(() => $"ReportSuppressions: {context.ReportedDiagnostics.Length}={string.Join("|", context.ReportedDiagnostics)}");

            var cancellationToken = context.CancellationToken;

            var analysisContext = new AnalysisContext();
            var runner = new SymbolicExecutionRunner(new NullPointerDereference(), context.CancellationToken, options.MaxSteps ?? 5000);
            runner.Initialize(analysisContext);

            var index = 0;

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

                    runner.PrepareAnalysis();

                    var stopwatch = Stopwatch.StartNew();
                    
                    var detectedDiagnostics = analysisContext.Analyze(elementNode, context, sourceTree);

                    var elapsed = stopwatch.ElapsedMilliseconds;
                    stopwatch.Stop();

                    logger.Log(() => $"  Analyzing {++index}: {elapsed} ms, {runner.Steps} steps, {detectedDiagnostics.Count} diagnostics");

                    var detected = detectedDiagnostics.FirstOrDefault(d => elementNodeLocation == d.Location);
                    if (detected?.Id != NullPointerDereference.NotNullDiagnosticId) 
                        continue;

                    var suppression = SupportedSuppressions.Single(item => item.SuppressedDiagnosticId == diagnostic.Id);
                    logger.Log(() => $"    ReportSuppression: {diagnostic}");
                    context.ReportSuppression(Suppression.Create(suppression, diagnostic));
                }
                catch (Exception ex)
                {
                    // could not analyze the full graph, so just do not suppress anything.
                    logger.Log($"  Error: {ex}");
                }
            }
        }
    }
}
