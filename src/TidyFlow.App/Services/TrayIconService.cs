using System.Windows;
using WinForms = System.Windows.Forms;

namespace TidyFlow.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _watchMenuItem;

    public TrayIconService(
        Action onOpenRequested,
        Action onToggleWatchRequested,
        Action onExitRequested)
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open TidyFlow", null, (_, _) => onOpenRequested());
        _watchMenuItem = new WinForms.ToolStripMenuItem("Start Watch Folder", null, (_, _) => onToggleWatchRequested());
        menu.Items.Add(_watchMenuItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit TidyFlow", null, (_, _) => onExitRequested());

        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "TidyFlow",
            Visible = true,
            ContextMenuStrip = menu
        };

        try
        {
            var iconResource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/tidyflow.ico"));
            if (iconResource is not null)
            {
                _notifyIcon.Icon = new System.Drawing.Icon(iconResource.Stream);
            }
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        _notifyIcon.DoubleClick += (_, _) => onOpenRequested();
    }

    public void UpdateWatchState(bool isWatching, string? watchedFolder = null)
    {
        _watchMenuItem.Text = isWatching ? "Stop Watch Folder" : "Start Watch Folder";
        _notifyIcon.Text = isWatching && !string.IsNullOrWhiteSpace(watchedFolder)
            ? $"TidyFlow — Watching {System.IO.Path.GetFileName(watchedFolder)}"
            : "TidyFlow";
    }

    public void ShowNotification(string title, string message, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
