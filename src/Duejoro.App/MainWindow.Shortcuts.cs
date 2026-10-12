// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;

namespace Duejoro.App;

public partial class MainWindow
{
    static readonly (string Group, (string Keys, string Does)[] Items)[] ShortcutList =
    [
        ("Items", [("Ctrl+N", "New item"), ("Enter", "Edit the chosen item"), ("Delete", "Delete the chosen item (Ctrl+Z brings it back)")]),
        ("Undoing", [("Ctrl+Z", "Undo the last change"), ("Ctrl+Y", "Redo")]),
        ("Finding", [("Ctrl+F", "Go to the search box"), ("Up / Down", "Move through the list")]),
    ];

    void OnShortcuts(object sender, RoutedEventArgs e)
    {
        var w = new Window
        {
            Title = "Keyboard Shortcuts", Width = 560, Height = 440, MinWidth = 420, MinHeight = 300, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        var dock = new DockPanel { Margin = new Thickness(24, 20, 24, 16) };
        var close = new Button { Content = "Close", IsDefault = true, IsCancel = true, Padding = new Thickness(18, 5, 18, 5), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(close, Dock.Bottom); dock.Children.Add(close);

        var panel = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        foreach (var (group, items) in ShortcutList)
        {
            panel.Children.Add(new TextBlock { Text = group, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 16, 0, 6) });
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (var i = 0; i < items.Length; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var keys = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 2, 12, 2), HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock { Text = items[i].Keys },
                };
                Grid.SetRow(keys, i); grid.Children.Add(keys);
                var does = new TextBlock { Text = items[i].Does, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 2) };
                Grid.SetRow(does, i); Grid.SetColumn(does, 1); grid.Children.Add(does);
            }
            panel.Children.Add(grid);
        }
        dock.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        w.Content = dock;
        w.ShowDialog();
    }
}
