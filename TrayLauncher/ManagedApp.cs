using System.Diagnostics;
using System.Windows.Forms;

namespace TrayLauncher;

/// <summary>
/// 1つの機能アプリの実行時状態(プロセス)を管理する。
/// 起動の二重防止は機能アプリ自身の Mutex に任せるため、ここでは関知しない。
/// </summary>
internal sealed class ManagedApp
{
    private const int StopTimeoutMs = 3000;

    public AppConfig Config { get; }
    private Process? _process;

    public ManagedApp(AppConfig config)
    {
        Config = config;
    }

    public bool IsRunning => _process is { HasExited: false };

    public void Start(JobObjectManager jobManager)
    {
        if (IsRunning)
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = Config.Path,
            Arguments = Config.Args,
            UseShellExecute = false,
        };

        try
        {
            _process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{Config.DisplayName} の起動に失敗しました。\n{ex.Message}",
                "TrayLauncher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (_process is null)
        {
            return;
        }

        // 代表アプリ(このプロセス)が終了したら道連れに終了させる。
        jobManager.Assign(_process.Handle);
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        using EventWaitHandle? exitSignal = TryOpenExitSignal();
        if (exitSignal is not null)
        {
            exitSignal.Set();
            if (_process!.WaitForExit(StopTimeoutMs))
            {
                return;
            }
        }

        // シグナルが届かない(古いバージョン等)/タイムアウトした場合は強制終了にフォールバックする。
        try
        {
            if (!_process!.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(StopTimeoutMs);
            }
        }
        catch (InvalidOperationException)
        {
            // 既に終了している
        }
    }

    public void Restart(JobObjectManager jobManager)
    {
        Stop();
        Start(jobManager);
    }

    private EventWaitHandle? TryOpenExitSignal()
    {
        try
        {
            return EventWaitHandle.OpenExisting(NamingScheme.ExitSignalName(Config.Name));
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
