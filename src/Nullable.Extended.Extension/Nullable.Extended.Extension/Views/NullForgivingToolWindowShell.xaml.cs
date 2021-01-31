using System.Composition;
using System.Windows;
using System.Windows.Media;
using PropertyChanged;
using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;

namespace Nullable.Extended.Extension.Views
{
    /// <summary>
    /// Interaction logic for NullForgivingToolWindowShell.
    /// </summary>
    [Export, Export(typeof(IThemeManager))]
    [AddINotifyPropertyChangedInterface]
    public partial class NullForgivingToolWindowShell : IThemeManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullForgivingToolWindowShell"/> class.
        /// </summary>
        public NullForgivingToolWindowShell(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();

            Resources.MergedDictionaries.Insert(0, TomsToolbox.Wpf.Styles.WpfStyles.GetDefaultStyles());
            Resources.MergedDictionaries.Add(DataTemplateManager.CreateDynamicDataTemplates(exportProvider));
        }

        public bool IsDarkTheme { get; private set; }

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
    }
}