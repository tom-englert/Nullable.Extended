using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullable.Extended.Analyzer.Test.Verifiers
{
    internal static class CSharpVerifierHelper
    {
        internal static readonly Dictionary<string, ReportDiagnostic> NullForgivingDetectionDiagnosticOptions = new()
        {
            { NullForgivingDetectionAnalyzer.GeneralDiagnosticId, ReportDiagnostic.Error },
            { NullForgivingDetectionAnalyzer.NullOrDefaultDiagnosticId, ReportDiagnostic.Error },
            { NullForgivingDetectionAnalyzer.LambdaDiagnosticId, ReportDiagnostic.Error },
        };
    }
}
