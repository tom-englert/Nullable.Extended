using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer.SonarAdapter
{
    public class AnalysisContext
    {
        private readonly Dictionary<SyntaxKind, Action<SyntaxNodeAnalysisContext>> _actions = new Dictionary<SyntaxKind, Action<SyntaxNodeAnalysisContext>>();
        private readonly Dictionary<SyntaxNode, IList<Diagnostic>> _cachedDiagnostics;

        public AnalysisContext(Dictionary<SyntaxNode, IList<Diagnostic>> cachedDiagnostics)
        {
            _cachedDiagnostics = cachedDiagnostics;
        }

        internal void RegisterSyntaxNodeAction(Action<SyntaxNodeAnalysisContext> action, params SyntaxKind[] syntaxKinds)
        {
            foreach (var syntaxKind in syntaxKinds)
            {
                _actions.Add(syntaxKind, action);
            }
        }

        public IList<Diagnostic> Analyze(SyntaxNode elementNode, SuppressionAnalysisContext context, SyntaxTree sourceTree)
        {
            var parentNode = elementNode;

            while (true)
            {
                parentNode = parentNode.Parent;
                if (parentNode == null)
                    throw new NotSupportedException("No supported node for analyzing found in tree. Aborted");

                if (!_actions.TryGetValue(parentNode.Kind(), out var action))
                    continue;

                var declaration = parentNode;

                if (_cachedDiagnostics.TryGetValue(declaration, out var cachedDiagnostics))
                {
                    return cachedDiagnostics;
                }

                var semanticModel = context.GetSemanticModel(sourceTree);

                IList<Diagnostic> detectedDiagnostics = new List<Diagnostic>();

                var syntaxNodeAnalysisContext = new SyntaxNodeAnalysisContext(
                    declaration,
                    null,
                    semanticModel,
                    null!,
                    diagnostic => detectedDiagnostics.Add(diagnostic),
                    _ => true,
                    context.CancellationToken
                );

                action(syntaxNodeAnalysisContext);

                _cachedDiagnostics.Add(declaration, detectedDiagnostics);
                
                return detectedDiagnostics;
            }
        }
    }
}
