﻿using System.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using DataGridExtensions;

using Nullable.Extended.Extension.AnalyzerFramework;

using TomsToolbox.Composition;
using TomsToolbox.Wpf.Composition;
using TomsToolbox.Wpf.Composition.AttributedModel;

namespace Nullable.Extended.Extension.Views
{
    /// <summary>
    /// Interaction logic for NullForgivingToolWindowView.xaml
    /// </summary>
    [DataTemplate(typeof(NullForgivingToolWindowViewModel))]
    [Shared]
    public partial class NullForgivingToolWindowView
    {
        public NullForgivingToolWindowView(IExportProvider exportProvider)
        {
            this.SetExportProvider(exportProvider);

            InitializeComponent();
        }

        private void DataGridRow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Space or Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                var item = (sender as DataGridRow)?.DataContext;
                if (item is AnalysisResult analysisResult)
                {
                    analysisResult.OpenInDocument();
                }
                e.Handled = true;
            }
        }

        private void DataGrid_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
                return;

            var lastFocusedCell = dataGrid.GetLastFocusedCell();

            if (true.Equals(e.NewValue) && lastFocusedCell != null)
            {
                _ = lastFocusedCell.Focus();
            }
        }
    }
}
