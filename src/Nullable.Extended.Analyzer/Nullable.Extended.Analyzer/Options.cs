using System.Globalization;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.CodeAnalysis.Diagnostics;

using static System.Net.Mime.MediaTypeNames;

namespace Nullable.Extended.Analyzer
{
    [Serializable]
    public class Options
    {
        private static readonly XmlSerializer Serializer = new(typeof(Options));

        public int? MaxSteps { get; set; }

        [XmlIgnore]
        public bool DisableSuppressions
        {
            get => bool.TryParse(DisableSuppressionsText, out var result) && result;
            set => DisableSuppressionsText = value.ToString(CultureInfo.InvariantCulture);
        }

        [XmlElement("DisableSuppressions")]
        public string? DisableSuppressionsText { get; set; }

        public static Options Read(AnalyzerConfigOptions configOptions)
        {
            try
            {
                if (configOptions.TryGetValue("build_property.nullableextendedanalyzer", out var options) && !string.IsNullOrEmpty(options))
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
            using var xmlReader = new XmlTextReader(stringReader);

            return (Options)Serializer.Deserialize(xmlReader);
        }
    }
}
