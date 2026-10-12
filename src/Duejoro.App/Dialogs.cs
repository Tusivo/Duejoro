// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Duejoro.App;

/// <summary>Small windows built in code: a message, a question, a line of text to type.</summary>
public static class Dialogs
{
    static readonly Brush NightBg = Frozen("#011627"), NightInk = Frozen("#D6DEEB");

    static Brush Frozen(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }

    /// <summary>True while the Night theme is chosen; windows opened from then on take its colors, as the main window does.</summary>
    public static bool Night { get; set; }

    /// <summary>Gives a window the colors of the chosen theme (Light and Dark come from the application's theme; Night is painted here).</summary>
    public static Window Style(Window w)
    {
        if (Night) { w.Background = NightBg; w.Foreground = NightInk; }
        return w;
    }

    public static Window Make(Window? owner, string title, double width = 460)
    {
        var w = new Window
        {
            Title = title, Width = width, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        if (owner is not null) w.Owner = owner;
        return Style(w);
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

    // our own message windows rather than the system message box, so they follow the chosen theme
    static bool Message(Window? owner, string title, string message, bool cancel)
    {
        var w = Make(owner, title, 520);
        var panel = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Buttons(w, "OK", cancel));
        w.Content = panel;
        return w.ShowDialog() == true;
    }

    public static void Info(Window? owner, string title, string message, bool error = false) =>
        Message(owner, title, message, cancel: false);

    public static bool Confirm(Window? owner, string title, string message) =>
        Message(owner, title, message, cancel: true);

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
