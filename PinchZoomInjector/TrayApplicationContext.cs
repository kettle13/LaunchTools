using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PinchZoomInjector;

/// <summary>
/// コンソールを持たない常駐アプリとして、タスクトレイにアイコンを表示する。
/// 右クリックメニューは「終了」のみ。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly Action _onExit;

    public TrayApplicationContext(Action onExit)
    {
        _onExit = onExit;

        var menu = new ContextMenuStrip();
        menu.Items.Add("終了", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "PinchZoomInjector",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private void ExitApplication()
    {
        _onExit();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }

    // 追加リソース不要にするため、赤/青の2点(ピンチの指)を模した小さな
    // アイコンをその場で描画する。
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.Red, 1, 5, 7, 7);
            g.FillEllipse(Brushes.DodgerBlue, 8, 5, 7, 7);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
