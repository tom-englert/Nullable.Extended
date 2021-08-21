using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private DataGridCell? _lastFocusedCell;

        public NullForgivingToolWindowView(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();
        }

        private void DataGridRow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                var item = (sender as DataGridRow)?.DataContext;
                if (item != null)
                {
                    (DataContext as NullForgivingToolWindowViewModel)?.OpenDocumentCommand.Execute(item);
                }
                e.Handled = true;
            }
        }

        private void Cell_GotFocus(object sender, RoutedEventArgs e)
        {
            _lastFocusedCell = sender as DataGridCell;
        }

        private void DataGrid_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (true.Equals(e.NewValue) && _lastFocusedCell != null)
            {
                _ = _lastFocusedCell.Focus();
            }
        }
    }
}
