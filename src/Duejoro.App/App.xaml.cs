// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using Duejoro.Core;

namespace Duejoro.App;

public partial class App : Application
{
    Mutex? single;
    EventWaitHandle? show;

    protected override void OnStartup(StartupEventArgs e)
    {
        // One copy per data folder: two copies writing the same records file would overwrite each other's changes.
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppPaths.DataFolder.ToLowerInvariant())))[..16];
        single = new Mutex(true, "Duejoro-" + id, out var first);
        show = new EventWaitHandle(false, EventResetMode.AutoReset, "Duejoro-show-" + id);
        if (!first)
        {
            show.Set();            // ask the running copy to come forward
            Shutdown();
            return;
        }
        var waiter = new Thread(() =>
        {
            while (show.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.ShowFromTray(needsAttention: false));
        }) { IsBackground = true };
        waiter.Start();
        base.OnStartup(e);
    }
}
