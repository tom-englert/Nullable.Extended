using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Nullable.Extended.Analyzer
{
    [Serializable]
    public class Options
    {
        private static readonly XmlSerializer Serializer = new(typeof(Options));

        public string? LogFile { get; set; }

        public int? MaxSteps { get; set; }

        public bool DisableSuppressions { get; set; }

        public static Options Read(AnalyzerConfigOptions configOptions)
        {
            try
            {
                if (configOptions.TryGetValue("build_property.nullableextendedanalyzer", out var options) &&
                    !string.IsNullOrEmpty(options))
                {
                    return Deserialize(options);
                }
            }
            catch
            {
                // just go with default options
            }

            return new Options();
        }

        public static Options Deserialize(string options)
        {
            using var stringReader = new StringReader($"<Options>{options}</Options>");
            using var xmlReader = new CaseInsensitiveXmlReader(stringReader);

            return (Options)Serializer.Deserialize(xmlReader);
        }

        private class CaseInsensitiveXmlReader : XmlTextReader
        {
            public CaseInsensitiveXmlReader(TextReader reader) : base(reader) { }

            public override string ReadElementString()
            {
                var text = base.ReadElementString();

                // bool TryParse accepts case-insensitive 'true' and 'false'
                if (bool.TryParse(text, out var result))
                {
                    text = XmlConvert.ToString(result);
                }

                return text;
            }
        }
    }
}