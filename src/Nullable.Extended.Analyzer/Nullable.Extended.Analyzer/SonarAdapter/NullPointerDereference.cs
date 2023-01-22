using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Rules.SymbolicExecution;
using SonarAnalyzer.SymbolicExecution;

namespace Nullable.Extended.Analyzer.SonarAdapter
{
    internal sealed class NullPointerDereference : ISymbolicExecutionAnalyzer
    {
        internal const string NullDiagnosticId = "Null";
        internal const string NotNullDiagnosticId = "NotNull";

        private static readonly DiagnosticDescriptor NullRule = new(NullDiagnosticId, string.Empty, string.Empty, string.Empty, DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NotNullRule = new(NotNullDiagnosticId, string.Empty, string.Empty, string.Empty, DiagnosticSeverity.Error, true);

        public IEnumerable<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(NullRule, NotNullRule);

        internal const string DiagnosticId = "Dummy";

        public ISymbolicExecutionAnalysisContext AddChecks(CSharpExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context) =>
            new AnalysisContext(explodedGraph, context);

        private sealed class AnalysisContext : ISymbolicExecutionAnalysisContext
        {
            private readonly CSharpExplodedGraph explodedGraph;
            private readonly SyntaxNodeAnalysisContext context;
            private readonly Dictionary<ExpressionSyntax, bool> identifiers = new();

            public AnalysisContext(CSharpExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context)
            {
                this.explodedGraph = explodedGraph;
                this.context = context;

                explodedGraph.MemberAccessed += MemberAccessedHandler;
            }

            public bool SupportsPartialResults => false;

            public IEnumerable<Diagnostic> GetDiagnostics() =>
                identifiers.Select(item => Diagnostic.Create(
                    item.Value ? NullRule : NotNullRule,
                    item.Key.GetLocation(),
                    item.Key.ToString()));

            public void Dispose()
            {
                explodedGraph.MemberAccessed -= MemberAccessedHandler;
            }

            private void MemberAccessedHandler(object sender, MemberAccessedEventArgs args) =>
                CollectMemberAccesses(args, context.SemanticModel);

            private void CollectMemberAccesses(MemberAccessedEventArgs args, SemanticModel semanticModel)
            {
                if (semanticModel.IsExtensionMethod(args.Identifier.Parent))
                    return;

                var existing = identifiers.TryGetValue(args.Identifier, out var maybeNull);
                if (!existing || !maybeNull)
                {
                    identifiers[args.Identifier] = args.MaybeNull;
                }
            }
        }
    }
}
