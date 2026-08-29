using System.Drawing;
using System.Runtime.InteropServices;

namespace PinchZoomInjector;

internal static class Program
{
    // --- チューニング用パラメータ ---
    private const double InitialRadius = 80.0;   // F13押下時の指の開き幅(px)
    // 半径は比率(指数)で変化するため、MinRadius を小さくしすぎると
    // そこからの回復に必要な絶対px量が小さくなり「戻らない」ように感じる。
    private const double MinRadius = 40.0;
    private const double MaxRadius = 500.0;
    private const double ZoomFactorPerNotch = 1.08; // ホイール1ノッチ(120)あたりの倍率
    private const double EaseFactor = 0.35;          // タイマー1tickごとの追従率
    private const int TickIntervalMs = 16;           // ~60Hz
    private const bool VerboseLogging = true;        // 半径の遷移をコンソールに出力(調整用)
    private const double RelayMargin = 10.0;          // Min/Max からこの距離まで来たらリレー(離して再タップ)

    private static readonly Settings AppSettings = Settings.Load();
    private static readonly PinchSimulator Pinch = new();
    private static readonly System.Windows.Forms.Timer AnimationTimer = new() { Interval = TickIntervalMs };

    // 解析用: 実際に注入している2点のタッチ座標を可視化するマーカー(PinchZoomInjector.ini の
    // ShowTouchMarkers=true で有効化)
    private static readonly TouchMarkerWindow LeftMarker = new(Color.Red);
    private static readonly TouchMarkerWindow RightMarker = new(Color.DodgerBlue);

    private static bool _triggerKeyDown;
    private static bool _touchStarted; // ホイールが来て実際に Pinch.Begin() 済みかどうか
    private static POINT _center;
    private static double _currentRadius;
    private static double _targetRadius;
    private static double _effectiveMaxRadius;
    private static bool _pendingRelayDown;

    // GCに回収されないようフィールドとして保持する
    private static readonly HookProc KeyboardProc = KeyboardHookCallback;
    private static readonly HookProc MouseProc = MouseHookCallback;
    private static IntPtr _keyboardHookHandle;
    private static IntPtr _mouseHookHandle;

    [STAThread]
    private static void Main()
    {
        // ディスプレイスケーリング(125%/150%等)環境で GetCursorPos と
        // InjectTouchInput の座標系がズレないよう、最初に明示的に設定する。
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        if (!NativeMethods.InitializeTouchInjection(2, NativeMethods.TOUCH_FEEDBACK_NONE))
        {
            Console.WriteLine($"InitializeTouchInjection に失敗しました: 0x{Marshal.GetLastWin32Error():X8}");
            return;
        }

        IntPtr hModule = NativeMethods.GetModuleHandle(null);
        _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, KeyboardProc, hModule, 0);
        _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, MouseProc, hModule, 0);

        if (_keyboardHookHandle == IntPtr.Zero || _mouseHookHandle == IntPtr.Zero)
        {
            Console.WriteLine($"フックの登録に失敗しました: 0x{Marshal.GetLastWin32Error():X8}");
            Cleanup();
            return;
        }

        AnimationTimer.Tick += (_, _) => AnimationTick();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cleanup();
            Environment.Exit(0);
        };

        Console.WriteLine($"起動しました。{AppSettings.TriggerKey} を押しながらホイールでピンチズーム、Ctrl+C で終了します。");
        System.Windows.Forms.Application.Run();
    }

    private static void UpdateMarkers(int centerX, int centerY, double radius)
    {
        if (!AppSettings.ShowTouchMarkers) return;
        LeftMarker.MoveToCenter(centerX - (int)radius, centerY);
        RightMarker.MoveToCenter(centerX + (int)radius, centerY);
    }

    private static void HideMarkers()
    {
        if (!AppSettings.ShowTouchMarkers) return;
        LeftMarker.HideMarker();
        RightMarker.HideMarker();
    }

    private static void AnimationTick()
    {
        // リレーで End() 済みの tick では IsActive が false になるが、
        // _pendingRelayDown が立っていれば次の Begin() 待ちなので処理を続ける。
        if (!Pinch.IsActive && !_pendingRelayDown) return;

        // 前 tick でリレー(離す)を発行済みなら、1 tick 空けてから中間半径で再タップする。
        // 間を空けるのは UP と DOWN を同一 tick で連続注入した場合に
        // Direct Manipulation 側が新規ジェスチャーと認識しない事態を避けるため。
        if (_pendingRelayDown)
        {
            Pinch.Begin(_center.X, _center.Y, _currentRadius);
            UpdateMarkers(_center.X, _center.Y, _currentRadius);
            _pendingRelayDown = false;
            if (VerboseLogging)
            {
                Console.WriteLine($"[Relay-Down] radius={_currentRadius:F1}");
            }
            return;
        }

        double diff = _targetRadius - _currentRadius;
        if (Math.Abs(diff) >= 0.5)
        {
            _currentRadius += diff * EaseFactor;
            Pinch.Update(_center.X, _center.Y, _currentRadius);
            UpdateMarkers(_center.X, _center.Y, _currentRadius);
            if (VerboseLogging)
            {
                Console.WriteLine($"[Update] currentRadius={_currentRadius:F1}");
            }
        }

        // Min/Max 端に近づいたら、一旦タッチを離して中間半径から挟み直す
        // (実際の指でピンチしきった後に持ち替える動作と同じ)。
        bool nearMin = _currentRadius <= MinRadius + RelayMargin;
        bool nearMax = _currentRadius >= _effectiveMaxRadius - RelayMargin;
        if (nearMin || nearMax)
        {
            Pinch.End();
            double resetRadius = Math.Clamp((MinRadius + _effectiveMaxRadius) / 2.0, MinRadius, _effectiveMaxRadius);
            _currentRadius = resetRadius;
            _targetRadius = resetRadius;
            _pendingRelayDown = true;
            if (VerboseLogging)
            {
                Console.WriteLine($"[Relay-Up] resetRadius={resetRadius:F1}");
            }
        }
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (info.vkCode == AppSettings.TriggerVirtualKeyCode)
            {
                int msg = wParam.ToInt32();
                if ((msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN) && !_triggerKeyDown)
                {
                    // ここではまだタッチダウンしない。押してすぐ離すだけの操作で
                    // 2本指タップ(=右クリック相当)と誤認識されるのを避けるため、
                    // 実際にホイールが来た時点(MouseHookCallback)で初めて Begin() する。
                    _triggerKeyDown = true;
                    _touchStarted = false;
                    NativeMethods.GetCursorPos(out _center);
                    _effectiveMaxRadius = ComputeMaxRadiusForCenter(_center);
                    _currentRadius = Math.Min(InitialRadius, _effectiveMaxRadius);
                    _targetRadius = _currentRadius;
                    if (VerboseLogging)
                    {
                        Console.WriteLine($"[Armed] center=({_center.X},{_center.Y}) effectiveMaxRadius={_effectiveMaxRadius:F1}");
                    }
                    return (IntPtr)1; // トリガーキー自体は他アプリに伝播させない
                }
                if ((msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP) && _triggerKeyDown)
                {
                    _triggerKeyDown = false;
                    if (_touchStarted)
                    {
                        AnimationTimer.Stop();
                        _pendingRelayDown = false;
                        Pinch.End();
                        HideMarkers();
                    }
                    _touchStarted = false;
                    return (IntPtr)1;
                }
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    // キーリピート中は握りつぶすだけ
                    return (IntPtr)1;
                }
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_MOUSEWHEEL && _triggerKeyDown)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            short delta = unchecked((short)((info.mouseData >> 16) & 0xFFFF));

            if (!_touchStarted)
            {
                _touchStarted = true;
                Pinch.Begin(_center.X, _center.Y, _currentRadius);
                UpdateMarkers(_center.X, _center.Y, _currentRadius);
                AnimationTimer.Start();
                if (VerboseLogging)
                {
                    Console.WriteLine($"[Begin] center=({_center.X},{_center.Y}) effectiveMaxRadius={_effectiveMaxRadius:F1}");
                }
            }

            _targetRadius *= Math.Pow(ZoomFactorPerNotch, delta / 120.0);
            _targetRadius = Math.Clamp(_targetRadius, MinRadius, _effectiveMaxRadius);

            if (VerboseLogging)
            {
                Console.WriteLine($"[Wheel] delta={delta} target={_targetRadius:F1} current={_currentRadius:F1}");
            }

            return (IntPtr)1; // 通常のスクロールとして処理させない
        }
        return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    // center を挟んで2点を左右に配置するため、画面(仮想デスクトップ)の
    // 左右端までの距離のうち短い方が実質的な半径上限になる。
    // これを超えると座標がクリップされ、ピンチアウトが効かなくなる。
    private static double ComputeMaxRadiusForCenter(POINT center)
    {
        int left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int right = left + width;

        double distanceToLeftEdge = center.X - left;
        double distanceToRightEdge = right - center.X;
        double screenLimit = Math.Max(0, Math.Min(distanceToLeftEdge, distanceToRightEdge) - 8); // 余白8px

        return Math.Max(MinRadius, Math.Min(MaxRadius, screenLimit));
    }

    private static void Cleanup()
    {
        AnimationTimer.Stop();
        if (Pinch.IsActive)
        {
            Pinch.End();
        }
        LeftMarker.Dispose();
        RightMarker.Dispose();
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }
        if (_mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }
    }
}
