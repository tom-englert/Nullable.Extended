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

        public void Initialize(SonarAnalysisContext context) =>
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

            // Some of the rules can return good results if the tree was only partially visited; others need to completely
            // walk the tree in order to avoid false positives.
            //
            // Due to this we split the rules in two sets and report the diagnostics in steps:
            // - When the tree is successfully visited and ExplorationEnded event is raised.
            // - When the tree visit ends (explodedGraph.Walk() returns). This will happen even if the maximum number of steps was
            // reached or if an exception was thrown during analysis.
            ReportDiagnostics(analyzerContexts, context, true);

            void ExplorationEndedHandler(object sender, EventArgs args)
            {
                finishedWithNoError = true;

                ReportDiagnostics(analyzerContexts, context, false);
            }

            if (!finishedWithNoError)
            {
                throw new NotSupportedException("Could not walk full graph.");
            }
        }

        private void ReportDiagnostics(IEnumerable<ISymbolicExecutionAnalysisContext> analyzerContexts, SyntaxNodeAnalysisContext context, bool supportsPartialResults)
        {
            foreach (var analyzerContext in analyzerContexts.Where(analyzerContext => analyzerContext.SupportsPartialResults == supportsPartialResults))
            {
                foreach (var diagnostic in analyzerContext.GetDiagnostics())
                {
                    context.ReportDiagnostic(diagnostic);
                }

                analyzerContext.Dispose();
            }
        }

        private IEnumerable<ISymbolicExecutionAnalysisContext> InitializeAnalyzers(CSharpExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context) =>
                new ISymbolicExecutionAnalysisContext[] { analyzer.AddChecks(explodedGraph, context) };
    }
}
