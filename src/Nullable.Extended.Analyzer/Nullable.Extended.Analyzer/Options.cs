using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer
{
    [Serializable]
    public class Options
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Options));

        public string? LogFile { get; set; }

        public int? MaxSteps { get; set; }

        public static Options Read(AnalyzerConfigOptions configOptions)
        {
            try
            {
                if (configOptions.TryGetValue("build_property.nullableextendedanalyzer", out var options) && !string.IsNullOrEmpty(options))
                {
                    using var xmlReader = XElement.Parse($"<Options>{options}</Options>").CreateReader();
                    return (Options)Serializer.Deserialize(xmlReader);
                }
            }
            catch { }

            return new Options();
        }
    }
}
