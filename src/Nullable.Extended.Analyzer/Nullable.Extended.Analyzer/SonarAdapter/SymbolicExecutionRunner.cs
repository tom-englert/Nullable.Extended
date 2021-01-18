/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2020 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Rules.SymbolicExecution;
using SonarAnalyzer.SymbolicExecution;

namespace Nullable.Extended.Analyzer.SonarAdapter
{
    public sealed class SymbolicExecutionRunner
    {
        private readonly ISymbolicExecutionAnalyzer analyzer;
        private readonly CancellationToken _cancellationToken;
        private readonly int _maxSteps;

        internal SymbolicExecutionRunner(ISymbolicExecutionAnalyzer analyzer, CancellationToken cancellationToken, int maxSteps)
        {
            this.analyzer = analyzer;
            _cancellationToken = cancellationToken;
            _maxSteps = maxSteps;
        }

        public void Initialize(AnalysisContext context) =>
            context.RegisterExplodedGraphBasedAnalysis(Analyze);

        public void PrepareAnalysis()
        {
            Steps = 0;
        }

        public int Steps { get; private set; }

        private void Analyze(CSharpExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context)
        {
            var analyzerContexts = InitializeAnalyzers(explodedGraph, context).ToList();

            var steps = explodedGraph.Walk(_maxSteps, _cancellationToken);

            Steps = Math.Max(Steps, steps);

            ReportDiagnostics(analyzerContexts, context);
        }

        private void ReportDiagnostics(IEnumerable<ISymbolicExecutionAnalysisContext> analyzerContexts, SyntaxNodeAnalysisContext context)
        {
            foreach (var analyzerContext in analyzerContexts)
            {
                foreach (var diagnostic in analyzerContext.GetDiagnostics())
                {
                    context.ReportDiagnostic(diagnostic);
                }

                analyzerContext.Dispose();
            }
        }

        private IEnumerable<ISymbolicExecutionAnalysisContext> InitializeAnalyzers(CSharpExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context) =>
                new[] { analyzer.AddChecks(explodedGraph, context) };
    }
}
