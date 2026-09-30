using System.Drawing.Drawing2D;

namespace F23Mute;

/// <summary>
/// コンソールを持たない常駐アプリとして動作させる。単独起動時(showTrayIcon=true)は
/// タスクトレイにアイコンを表示し、右クリックメニューの「終了」から終了できる。
/// LaunchTools経由での起動時(showTrayIcon=false)はアイコンを出さず、
/// LaunchToolsIntegration からの終了シグナルで ExitApplication() が呼ばれるのを待つ。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon? _trayIcon;
    private readonly Action _onExit;

    public TrayApplicationContext(Action onExit, bool showTrayIcon)
    {
        _onExit = onExit;

        if (showTrayIcon)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("終了", null, (_, _) => ExitApplication());

            _trayIcon = new NotifyIcon
            {
                Icon = CreateIcon(),
                Text = "F23Mute",
                ContextMenuStrip = menu,
                Visible = true,
            };
        }
    }

    public void ExitApplication()
    {
        _onExit();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.ExitThreadCore();
    }

    // 追加リソース不要にするため、スピーカーに赤い×を重ねた(=ミュート)
    // 小さなアイコンをその場で描画する。
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPolygon(Brushes.DimGray, new PointF[]
            {
                new(1, 5), new(4, 5), new(8, 1), new(8, 15), new(4, 11), new(1, 11),
            });
            using var pen = new Pen(Color.Red, 2);
            g.DrawLine(pen, 10, 5, 15, 11);
            g.DrawLine(pen, 15, 5, 10, 11);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
