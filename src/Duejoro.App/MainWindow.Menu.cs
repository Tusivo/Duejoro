// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Duejoro.Core;

namespace Duejoro.App;

public partial class MainWindow
{
    // ---- help ---------------------------------------------------------------------------------------------------------

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { MessageBox.Show("Windows did not open the link. The address is " + url, AppInfo.Name); }
    }

    void OnReportProblem(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.Repository + "/issues/new/choose");

    void OnRequestFeature(object sender, RoutedEventArgs e)
    {
        var body = "What would you like Duejoro to do, and how would it help?\r\n\r\n\r\n(Please do not include private details.)\r\n\r\nDuejoro " + AppInfo.Version;
        OpenUrl($"mailto:{AppInfo.ContactEmail}?subject={Uri.EscapeDataString("Duejoro feature request")}&body={Uri.EscapeDataString(body)}");
    }

    void OnSupport(object sender, RoutedEventArgs e) => OpenUrl("https://buymeacoffee.com/tusivo");

    void OnAbout(object sender, RoutedEventArgs e)
    {
        var w = new Window
        {
            Title = "About Duejoro", Width = 720, Height = 560, MinWidth = 560, MinHeight = 400, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        var dock = new DockPanel { Margin = new Thickness(26, 22, 26, 18) };

        var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.Children.Add(new System.Windows.Controls.Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/Duejoro.png")),
            Width = 72, Height = 72, Margin = new Thickness(0, 0, 18, 0),
        });
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = AppInfo.Name, FontSize = 30, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(new TextBlock { Text = "Version " + AppInfo.Version + (AppInfo.IsPreRelease ? "   (alpha: keep your own backups too)" : ""), Opacity = 0.8 });
        titles.Children.Add(new TextBlock { Text = "License: " + AppInfo.License, Opacity = 0.8, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(titles, 1); head.Children.Add(titles);
        DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        Button Link(string text, string url) { var b = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0) }; b.Click += (_, _) => OpenUrl(url); return b; }
        buttons.Children.Add(Link("Source Code", AppInfo.Repository));
        buttons.Children.Add(Link("Website", AppInfo.Website));
        buttons.Children.Add(Link("Support the Project", "https://buymeacoffee.com/tusivo"));
        buttons.Children.Add(new Button { Content = "Close", IsDefault = true, IsCancel = true, Padding = new Thickness(16, 5, 16, 5), Margin = new Thickness(6, 0, 0, 0) });
        DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);

        var body = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        TextBlock Heading(string t) => new() { Text = t, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 4) };
        TextBlock Para(string t) => new() { Text = t, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        body.Children.Add(Para("Free, open-source tracker for the dates your business cannot afford to miss: licenses, permits, insurance, certifications, contracts, leases, subscriptions, warranties and filings. Everything stays on your own computer."));
        body.Children.Add(Heading("Where Duejoro keeps its files"));
        body.Children.Add(Para(AppPaths.IsPortable
            ? "Beside the program, in a folder called Duejoro-Data. It holds your records, the files you attach and automatic backups, so copying Duejoro.exe together with that folder moves everything:"
            : "In this folder (Duejoro could not write beside the program, so it uses your account's own folder):"));
        var pathRow = new DockPanel();
        var open = new Button { Content = "Open Folder", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(open, Dock.Right);
        open.Click += (_, _) => OpenFolder(store.DataFolder);
        pathRow.Children.Add(open);
        pathRow.Children.Add(new TextBox { Text = store.DataFolder, IsReadOnly = true });
        body.Children.Add(pathRow);
        body.Children.Add(Para("Back this folder up with the rest of your business files. A dated backup of the records is also made automatically (in the Backups folder inside it). The only network use is Check for Updates, which you start yourself."));
        body.Children.Add(Heading("Questions or ideas"));
        body.Children.Add(Para("Write to " + AppInfo.ContactEmail + " or open an issue on GitHub. Duejoro is a Tusivo project."));
        dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        w.Content = dock;
        w.ShowDialog();
    }

    // ---- updates ------------------------------------------------------------------------------------------------------

    async void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        MessageStatus.Text = "Looking for updates...";
        try
        {
            var release = await Updater.CheckAsync(AppInfo.Version);
            if (release is null)
            {
                MessageStatus.Text = "";
                Dialogs.Info(this, "Check for Updates", $"You have the newest version ({AppInfo.Version}).");
                return;
            }
            var notes = release.Notes.Trim();
            if (notes.Length > 500) notes = notes[..500].TrimEnd() + " …";
            if (!Dialogs.Confirm(this, "Check for Updates",
                $"Version {release.Version} is available (you have {AppInfo.Version}).\n\n{(notes.Length > 0 ? PlainText.FromMarkdown(notes) + "\n\n" : "")}" +
                "Download and install it now? Duejoro keeps your current copy as a backup in the same folder, and only installs an update that carries Duejoro's release signature. Your records are not touched.")) { MessageStatus.Text = ""; return; }
            var exe = Environment.ProcessPath ?? throw new UpdateException("Duejoro could not find its own file.");
            var progress = new Progress<int>(p => MessageStatus.Text = $"Downloading Duejoro {release.Version} ... {p}%");
            var file = await Updater.DownloadAsync(release, exe, progress);
            var backup = Updater.Install(file, exe, AppInfo.Version);
            MessageStatus.Text = $"Installed version {release.Version}.";
            if (Dialogs.Confirm(this, "Check for Updates", $"Version {release.Version} is installed. Your previous version was kept as {Path.GetFileName(backup)}.\n\nClose and reopen Duejoro now?"))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageStatus.Text = "";
            Dialogs.Info(this, "Check for Updates", Updater.Friendly(ex), error: true);
        }
    }
}
