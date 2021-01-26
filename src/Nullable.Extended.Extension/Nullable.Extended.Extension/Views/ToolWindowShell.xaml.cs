using System.Composition;
using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;

namespace Nullable.Extended.Extension.Views
{
    /// <summary>
    /// Interaction logic for ToolWindowShell.
    /// </summary>
    [Export]
    public partial class ToolWindowShell
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindowShell"/> class.
        /// </summary>
        public ToolWindowShell(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();

            Resources.MergedDictionaries.Insert(0, TomsToolbox.Wpf.Styles.WpfStyles.GetDefaultStyles());
            Resources.MergedDictionaries.Add(DataTemplateManager.CreateDynamicDataTemplates(exportProvider));
        }
    }
}