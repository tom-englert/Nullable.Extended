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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Rules.SymbolicExecution;
using SonarAnalyzer.SymbolicExecution;

namespace Nullable.Extended.Analyzer.SonarAdapter
{
    public sealed class SymbolicExecutionRunner
    {
        private readonly ISymbolicExecutionAnalyzer analyzer;

        internal SymbolicExecutionRunner(ISymbolicExecutionAnalyzer analyzer)
        {
            this.analyzer = analyzer;
        }

        public void Initialize(AnalysisContext context) =>
            context.RegisterExplodedGraphBasedAnalysis(Analyze);

        private void Analyze(CSharpExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context)
        {
            var finishedWithNoError = false;

            var analyzerContexts = InitializeAnalyzers(explodedGraph, context).ToList();

            try
            {
                explodedGraph.ExplorationEnded += ExplorationEndedHandler;

                explodedGraph.Walk();
            }
            finally
            {
                explodedGraph.ExplorationEnded -= ExplorationEndedHandler;
            }

            void ExplorationEndedHandler(object sender, EventArgs args)
            {
                finishedWithNoError = true;
                ReportDiagnostics(analyzerContexts, context);
            }

            if (!finishedWithNoError)
            {
                throw new NotSupportedException("Could not walk full graph.");
            }
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
                new [] { analyzer.AddChecks(explodedGraph, context) };
    }
}
