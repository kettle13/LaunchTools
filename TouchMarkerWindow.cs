using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PinchZoomInjector;

/// <summary>
/// 解析用: 注入しているタッチ座標を可視化する小さな円形ウィンドウ。
/// クリックを透過し、フォーカスも奪わないため通常の操作の妨げにならない。
/// </summary>
internal sealed class TouchMarkerWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000; // フォーカスを奪わない
    private const int WS_EX_TRANSPARENT = 0x00000020; // マウス操作を下のウィンドウへ透過
    private const int WS_EX_TOOLWINDOW = 0x00000080;  // タスクバー/Alt+Tabに出さない
    private const int WS_EX_LAYERED = 0x00080000;

    private const int MarkerSize = 24;

    public TouchMarkerWindow(Color color)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(MarkerSize, MarkerSize);
        BackColor = color;

        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, Width, Height);
        Region = new Region(path);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            return cp;
        }
    }

    public void MoveToCenter(int centerX, int centerY)
    {
        var newLocation = new Point(centerX - MarkerSize / 2, centerY - MarkerSize / 2);
        if (Location != newLocation)
        {
            Location = newLocation;
        }
        if (!Visible)
        {
            Show();
        }
    }

    public void HideMarker()
    {
        if (Visible)
        {
            Hide();
        }
    }
}
