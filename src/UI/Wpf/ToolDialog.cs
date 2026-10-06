using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MEP_Sheet_Manager
{
    // Snapshot-only forms. Revit access remains in the parent's ExternalEvent callback.
    internal sealed class ToolDialog : Window
    {
        public readonly StackPanel Fields = new StackPanel();
        public readonly StackPanel Actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        public ToolDialog(Window owner, string title, string hint, double width = 620)
        {
            Owner = owner; Title = title; Width = width; Height = 530; MinHeight = 360; MinWidth = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(245, 242, 238)); FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
            var dock = new DockPanel { Margin = new Thickness(20) }; Content = dock;
            var heading = new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
            DockPanel.SetDock(heading, Dock.Top); dock.Children.Add(heading);
            DockPanel.SetDock(Actions, Dock.Bottom); dock.Children.Add(Actions);
            dock.Children.Add(new ScrollViewer { Content = Fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }
        public TextBox Text(string label, string value = "") { Label(label); var box = new TextBox { Text = value, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 10) }; Fields.Children.Add(box); return box; }
        public ComboBox Combo(string label, System.Collections.IEnumerable values, string display = null) { Label(label); var box = new ComboBox { ItemsSource = values, DisplayMemberPath = display ?? "", Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(6), SelectedIndex = 0 }; Fields.Children.Add(box); return box; }
        public void Label(string label) { Fields.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5), TextWrapping = TextWrapping.Wrap }); }
        public DataGrid Table(System.Collections.IEnumerable rows, params string[] properties) {
            var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, MinHeight = 180, MaxHeight = 280, SelectionMode = DataGridSelectionMode.Single, Margin = new Thickness(0, 4, 0, 12) };
            foreach (var p in properties) grid.Columns.Add(new DataGridTextColumn { Header = p, Binding = new Binding(p), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            Fields.Children.Add(grid); grid.SelectedIndex = 0; return grid;
        }
        public Button Button(string label, Action action) { var button = new Button { Content = label, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(8, 14, 0, 0) }; button.Click += (s, e) => action(); Actions.Children.Add(button); return button; }
        public bool Confirm(string label = "Áp dụng") { Button("Hủy", () => DialogResult = false); Button(label, () => DialogResult = true); return ShowDialog() == true; }
    }
}
