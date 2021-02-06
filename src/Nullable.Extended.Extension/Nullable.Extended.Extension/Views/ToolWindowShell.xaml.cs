using System.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;
using TomsToolbox.Wpf.Composition.XamlExtensions;

namespace Nullable.Extended.Extension.Views
{
    /// <summary>
    /// Interaction logic for ToolWindowShell.
    /// </summary>
    [Export, Export(typeof(IThemeManager))]
    public partial class ToolWindowShell : IThemeManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindowShell"/> class.
        /// </summary>
        public ToolWindowShell(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();

            Resources.MergedDictionaries.Add(DataTemplateManager.CreateDynamicDataTemplates(exportProvider));
        }

        public static readonly DependencyProperty IsDarkThemeProperty = DependencyProperty.Register(
            "IsDarkTheme", typeof(bool), typeof(ToolWindowShell), new PropertyMetadata(default(bool)));

        public bool IsDarkTheme
        {
            get => (bool)GetValue(IsDarkThemeProperty);
            set => SetValue(IsDarkThemeProperty, value);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if ((e.Property != ForegroundProperty) && (e.Property != BackgroundProperty))
                return;

            var foreground = ((Foreground as SolidColorBrush)?.Color).ToGray();
            var background = ((Background as SolidColorBrush)?.Color).ToGray();

            IsDarkTheme = background < foreground;
        }
    }

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
    }
}