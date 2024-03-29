﻿using System.Composition;
using System.Windows;
using System.Windows.Media;

using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;
using TomsToolbox.Wpf.Styles;

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
            Resources.MergedDictionaries.Insert(0, WpfStyles.GetDefaultStyles());
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
}