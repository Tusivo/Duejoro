// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;

namespace Duejoro.App;

/// <summary>Small windows built in code: a message, a question, a line of text to type.</summary>
public static class Dialogs
{
    public static Window Make(Window? owner, string title, double width = 460)
    {
        var w = new Window
        {
            Title = title, Width = width, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        if (owner is not null) w.Owner = owner;
        return w;
    }

    public static StackPanel Buttons(Window w, string ok, bool cancel, Func<bool>? onOk = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var okButton = new Button { Content = ok, IsDefault = true, Padding = new Thickness(18, 6, 18, 6), MinWidth = 88 };
        okButton.Click += (_, _) => { if (onOk is null || onOk()) w.DialogResult = true; };
        row.Children.Add(okButton);
        if (cancel)
            row.Children.Add(new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(18, 6, 18, 6), MinWidth = 88, Margin = new Thickness(8, 0, 0, 0) });
        return row;
    }

    public static void Info(Window? owner, string title, string message, bool error = false) =>
        MessageBox.Show(owner, message, title, MessageBoxButton.OK, error ? MessageBoxImage.Warning : MessageBoxImage.Information);

    public static bool Confirm(Window? owner, string title, string message) =>
        MessageBox.Show(owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    public static string? Ask(Window? owner, string title, string message, string initial = "", bool multiline = false)
    {
        var w = Make(owner, title);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        var input = new TextBox
        {
            Text = initial, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 90 : 0, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
        };
        panel.Children.Add(input);
        panel.Children.Add(Buttons(w, "OK", true));
        w.Content = panel;
        w.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return w.ShowDialog() == true ? input.Text : null;
    }
}
