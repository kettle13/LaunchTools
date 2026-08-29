using System.Runtime.InteropServices;

namespace TrayLauncher;

/// <summary>
/// 起動した機能アプリを Windows Job Object に割り当てる。
/// 代表アプリ(このプロセス)が正常/異常問わず終了すると、OSがハンドルを
/// クローズするタイミングで割り当て済みの子プロセスも自動的に終了する。
/// </summary>
internal sealed class JobObjectManager : IDisposable
{
    private readonly IntPtr _jobHandle;

    public JobObjectManager()
    {
        _jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (_jobHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateJobObject に失敗しました: 0x{Marshal.GetLastWin32Error():X8}");
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        bool ok = NativeMethods.SetInformationJobObject(
            _jobHandle,
            NativeMethods.JobObjectExtendedLimitInformation,
            ref info,
            (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());

        if (!ok)
        {
            throw new InvalidOperationException($"SetInformationJobObject に失敗しました: 0x{Marshal.GetLastWin32Error():X8}");
        }
    }

    public void Assign(IntPtr processHandle)
    {
        if (!NativeMethods.AssignProcessToJobObject(_jobHandle, processHandle))
        {
            // 既に別のJobに割り当て済みのプロセス等では失敗しうるが、致命的ではないため無視する。
        }
    }

    public void Dispose()
    {
        if (_jobHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_jobHandle);
        }
    }
}
