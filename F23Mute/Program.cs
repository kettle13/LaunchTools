using System.Runtime.InteropServices;

namespace F23Mute;

/// <summary>
/// F23 を押している間だけ再生デバイスをミュートする。
/// 設定ファイルは持たない。単独起動時はトレイアイコンの「終了」で、
/// LaunchTools経由(--no-tray)の場合は LaunchTools からの終了シグナルで終了する。
///
/// 起動引数:
///   --no-tray         トレイアイコンを出さず、LaunchTools からの終了シグナルを待つ。
///   --device &lt;名前&gt;  ミュート対象の再生デバイス名(部分一致、大文字小文字無視)。複数回指定可。
///                      省略時は既定の再生デバイスが対象。
/// </summary>
internal static class Program
{
    // GCに回収されないようフィールドとして保持する
    private static readonly HookProc KeyboardProc = KeyboardHookCallback;
    private static IntPtr _keyboardHookHandle;
    private static SynchronizationContext? _uiContext;
    private static IReadOnlyList<string> _deviceNames = [];

    [STAThread]
    private static void Main(string[] args)
    {
        if (!LaunchToolsIntegration.TryAcquireSingleInstance())
        {
            // 既に起動中なので、このプロセスは即終了する。
            return;
        }

        _deviceNames = ParseDeviceNames(args);
        bool noTray = args.Any(a => string.Equals(a, "--no-tray", StringComparison.OrdinalIgnoreCase));

        // 終了シグナルはバックグラウンドスレッドで受け取るため、UIスレッド(ここ)への
        // マーシャリング用に明示的にコンテキストを用意しておく。
        _uiContext = new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_uiContext);

        _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, KeyboardProc, NativeMethods.GetModuleHandle(null), 0);
        if (_keyboardHookHandle == IntPtr.Zero)
        {
            MessageBox.Show(
                $"フックの登録に失敗しました: 0x{Marshal.GetLastWin32Error():X8}",
                "F23Mute",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        var trayContext = new TrayApplicationContext(Cleanup, showTrayIcon: !noTray);

        if (noTray)
        {
            LaunchToolsIntegration.StartExitSignalListener(() => _uiContext.Post(_ => trayContext.ExitApplication(), null));
        }

        Application.Run(trayContext);
    }

    private static void Cleanup()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (info.vkCode == NativeMethods.VK_F23)
            {
                int msg = wParam.ToInt32();
                bool? mute = msg switch
                {
                    NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN => true,
                    NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP => false,
                    _ => null,
                };
                if (mute is bool m)
                {
                    // COM呼び出しでフックの応答が遅れるとWindowsにフックを外されるため、
                    // フックからは即座に戻り、ミュート操作はメッセージループ側で行う。
                    _uiContext!.Post(_ => SystemAudio.SetMute(m, _deviceNames), null);
                }
                return (IntPtr)1; // F23 自体は他アプリに伝播させない
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private static List<string> ParseDeviceNames(string[] args)
    {
        var names = new List<string>();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--device", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                names.Add(args[++i].Trim());
            }
        }
        return names;
    }
}
