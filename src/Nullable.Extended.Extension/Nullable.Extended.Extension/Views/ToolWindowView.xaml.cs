using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;
using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    /// <summary>
    /// Interaction logic for ToolWindowView.xaml
    /// </summary>
    [DataTemplate(typeof(ToolWindowViewModel))]
    public partial class ToolWindowView
    {
        public ToolWindowView(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();
        }
    }
}
