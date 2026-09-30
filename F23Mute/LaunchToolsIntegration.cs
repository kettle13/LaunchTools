using System.Diagnostics;

namespace F23Mute;

/// <summary>
/// LaunchTools(代表アプリ)経由での起動に対応するための最小限の統合コード。
/// 他の機能アプリへもそのままコピペで展開できるよう、依存を増やさず1ファイルに収める。
///
/// 命名規則はTrayLauncher側の NamingScheme と一致させること:
///   Mutex        : Local\LaunchTools_{AppName}_SingleInstance
///   終了シグナル : Local\LaunchTools_{AppName}_ExitSignal
/// AppName は実行ファイル名(拡張子なし)。TrayLauncherのconfigの [App:Name] と一致させる。
/// </summary>
internal static class LaunchToolsIntegration
{
    private static Mutex? _singleInstanceMutex;

    private static string AppName => Process.GetCurrentProcess().ProcessName;

    /// <summary>
    /// Main冒頭で呼ぶ。false が返ったら、既に起動中なので即座に終了すること。
    /// </summary>
    public static bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, $"Local\\LaunchTools_{AppName}_SingleInstance", out bool createdNew);
        return createdNew;
    }

    /// <summary>
    /// --no-tray 指定時に呼ぶ。終了シグナルを別スレッドで待機し、Setされたら onExit を呼ぶ。
    /// onExit はバックグラウンドスレッドから呼ばれるため、呼び出し側でUIスレッドへの
    /// マーシャリング(SynchronizationContext.Post等)を行うこと。
    /// </summary>
    public static void StartExitSignalListener(Action onExit)
    {
        var exitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, $"Local\\LaunchTools_{AppName}_ExitSignal");
        var thread = new Thread(() =>
        {
            exitSignal.WaitOne();
            onExit();
        })
        {
            IsBackground = true,
        };
        thread.Start();
    }
}
