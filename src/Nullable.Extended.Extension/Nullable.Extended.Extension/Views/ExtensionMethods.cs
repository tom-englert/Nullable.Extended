using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

using Community.VisualStudio.Toolkit;

using Microsoft.VisualStudio.Text;

using Nullable.Extended.Extension.AnalyzerFramework;

using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition.XamlExtensions;

namespace Nullable.Extended.Extension.Views
{
    public static class ExtensionMethods
    {
        public static double ToGray(this Color? color)
        {
            return color?.R * 0.3 + color?.G * 0.6 + color?.B * 0.1 ?? 0.0;
        }

        public static Control GetToolWindowShell(this IExportProvider exportProvider, string regionId)
        {
            var shell = exportProvider.GetExportedValue<ToolWindowShell>();

            VisualComposition.SetRegionId(shell, regionId);

            return shell;
        }


#pragma warning disable VSTHRD100 // Avoid async void methods
        public static async void OpenInDocument(this AnalysisResult result)
        {
            try
            {
                var textView = (await VS.Documents.OpenAsync(result.FilePath).ConfigureAwait(true))?.TextView;
                if (textView == null)
                    return;

                var snapshot = textView.FormattedLineSource.SourceTextSnapshot;
                var line = snapshot.Lines.Skip(result.Line - 1).FirstOrDefault();
                var span = new SnapshotSpan(new VirtualSnapshotPoint(line, result.Column - 1).Position, new VirtualSnapshotPoint(line, result.Column).Position);

                textView.Selection.Select(span, false);
                textView.ViewScroller.EnsureSpanVisible(span);
            }
            catch
            {
                // Probably the document has already changed and the span is no longer valid. User can simply retry.
            }
        }
    }
}
