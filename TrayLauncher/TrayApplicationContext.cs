using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TrayLauncher;

/// <summary>
/// 代表アプリのタスクトレイ本体。アプリごとに「起動中/停止中」表示と
/// 起動/再起動/終了のサブメニューを提供する。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly JobObjectManager _jobManager;
    private readonly List<ManagedApp> _apps;
    private readonly ContextMenuStrip _menu;

    public TrayApplicationContext(IReadOnlyList<AppConfig> configs)
    {
        _jobManager = new JobObjectManager();
        _apps = configs.Select(c => new ManagedApp(c)).ToList();

        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();
        RebuildMenu();

        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "LaunchTools",
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        foreach (ManagedApp app in _apps)
        {
            string status = app.IsRunning ? "起動中" : "停止中";
            var appMenu = new ToolStripMenuItem($"{app.Config.DisplayName} ({status})");

            appMenu.DropDownItems.Add(new ToolStripMenuItem("起動", null, (_, _) => app.Start(_jobManager))
            {
                Enabled = !app.IsRunning,
            });
            appMenu.DropDownItems.Add(new ToolStripMenuItem("再起動", null, (_, _) => app.Restart(_jobManager))
            {
                Enabled = app.IsRunning,
            });
            appMenu.DropDownItems.Add(new ToolStripMenuItem("終了", null, (_, _) => app.Stop())
            {
                Enabled = app.IsRunning,
            });

            _menu.Items.Add(appMenu);
        }

        if (_apps.Count > 0)
        {
            _menu.Items.Add(new ToolStripSeparator());
        }

        _menu.Items.Add(new ToolStripMenuItem("終了", null, (_, _) => ExitApplication()));
    }

    private void ExitApplication()
    {
        // まず各アプリへグレースフルシャットダウンを試みる。
        // JobObject の KILL_ON_JOB_CLOSE はあくまで異常終了時の安全網として残す。
        foreach (ManagedApp app in _apps)
        {
            app.Stop();
        }
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _jobManager.Dispose();
        base.ExitThreadCore();
    }

    // ランチャーらしい2x2グリッドのアイコンをその場で描画する(追加リソース不要)。
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(Brushes.SlateGray, 1, 1, 6, 6);
            g.FillRectangle(Brushes.SlateGray, 9, 1, 6, 6);
            g.FillRectangle(Brushes.SlateGray, 1, 9, 6, 6);
            g.FillRectangle(Brushes.SlateGray, 9, 9, 6, 6);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
