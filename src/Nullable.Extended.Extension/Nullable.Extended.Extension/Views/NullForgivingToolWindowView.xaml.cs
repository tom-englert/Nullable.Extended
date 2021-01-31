using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;
using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    /// <summary>
    /// Interaction logic for NullForgivingToolWindowView.xaml
    /// </summary>
    [DataTemplate(typeof(NullForgivingToolWindowViewModel))]
    public partial class NullForgivingToolWindowView
    {
        public NullForgivingToolWindowView(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();
        }
    }
}
