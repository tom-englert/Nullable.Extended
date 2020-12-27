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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer.SonarAdapter
{
    /// <summary>
    /// SonarC# and SonarVB specific context for initializing an analyzer. This type acts as a wrapper around Roslyn
    /// <see cref="AnalysisContext"/> to allow for specialized control over the analyzer.
    /// Here is the list of fine-grained changes we are doing:
    /// - Avoid duplicated issues when the analyzer NuGet (SonarAnalyzer) and the VSIX (SonarLint) are installed simultaneously.
    /// - Allow a specific kind of rule-set for SonarLint (enable/disable a rule).
    /// - Prevent reporting an issue when it was suppressed on SonarQube.
    /// </summary>
    public class SonarAnalysisContext
    {
        private readonly Dictionary<SyntaxKind, Action<SyntaxNodeAnalysisContext>> _actions = new Dictionary<SyntaxKind, Action<SyntaxNodeAnalysisContext>>();
        private readonly Dictionary<SyntaxNode, IList<Diagnostic>> _cachedDiagnostics = new Dictionary<SyntaxNode, IList<Diagnostic>>();

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
