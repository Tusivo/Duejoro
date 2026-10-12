// SPDX-License-Identifier: AGPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Duejoro.Core;
using WinForms = System.Windows.Forms;

namespace Duejoro.App;

/// <summary>
/// The optional notification-area icon. When the person turns it on, closing the window keeps Duejoro running quietly, and it tells them
/// (at most once a day, or again when something new needs attention) what is overdue or due soon. File > Exit really quits.
/// </summary>
public partial class MainWindow
{
    WinForms.NotifyIcon? tray;
    DispatcherTimer? trayTimer;
    bool exiting;
    string lastToast = "";

    void InitTray()
    {
        TrayItem.IsChecked = settings.TrayReminders;
        Application.Current.SessionEnding += (_, _) => exiting = true;
        if (settings.TrayReminders) EnsureTray();
    }

    void OnTrayToggle(object sender, RoutedEventArgs e)
    {
        settings.SetTrayReminders(TrayItem.IsChecked);
        if (TrayItem.IsChecked)
        {
            EnsureTray();
            MessageStatus.Text = "Closing the window now keeps Duejoro in the notification area. Use File > Exit to quit.";
        }
        else
        {
            DisposeTray();
            MessageStatus.Text = "Closing the window now quits Duejoro.";
        }
    }

    void EnsureTray()
    {
        if (tray is not null) return;
        try
        {
            using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Duejoro.ico"))!.Stream;
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Open Duejoro", null, (_, _) => ShowFromTray(needsAttention: false));
            menu.Items.Add("Exit", null, (_, _) => { exiting = true; Close(); });
            tray = new WinForms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(stream),
                Text = "Duejoro",
                ContextMenuStrip = menu,
                Visible = true,
            };
            tray.DoubleClick += (_, _) => ShowFromTray(needsAttention: false);
            tray.BalloonTipClicked += (_, _) => ShowFromTray(needsAttention: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            tray = null;
            return;
        }
        trayTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        trayTimer.Tick += (_, _) => RemindFromTray();
        trayTimer.Start();
    }

    void DisposeTray()
    {
        trayTimer?.Stop(); trayTimer = null;
        if (tray is null) return;
        tray.Visible = false;
        tray.Dispose();
        tray = null;
    }

    void HideToTray()
    {
        EnsureTray();
        if (tray is null) { exiting = true; Close(); return; }   // no icon could be made: quit rather than vanish
        Hide();
        RemindFromTray();
    }

    /// <summary>Bring the window back (also used when a second copy of the program is started).</summary>
    internal void ShowFromTray(bool needsAttention)
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (needsAttention) StatusBox.SelectedIndex = 1;
    }

    void RemindFromTray()
    {
        if (tray is null || IsVisible && WindowState != WindowState.Minimized) return;
        var today = Today;
        var active = store.Items.Where(o => !o.Done).ToList();
        var over = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.Overdue);
        var soon = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.DueSoon);
        if (over + soon == 0) return;
        var key = $"{today:yyyy-MM-dd}|{over}|{soon}";
        if (key == lastToast) return;
        lastToast = key;
        var parts = new List<string>();
        if (over > 0) parts.Add($"{over} overdue");
        if (soon > 0) parts.Add($"{soon} due soon");
        tray.ShowBalloonTip(10000, "Duejoro", string.Join(" and ", parts) + ". Click to see them.", WinForms.ToolTipIcon.Warning);
    }
}
