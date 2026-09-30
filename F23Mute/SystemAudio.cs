using System.Runtime.InteropServices;

namespace F23Mute;

/// <summary>
/// Windows Core Audio API で再生デバイスのミュートを切り替える。
/// デバイス名の指定が無ければ既定の再生デバイス(タスクバーのスピーカーアイコンと同じもの)、
/// 指定があれば名前に部分一致する有効な再生デバイスすべてが対象。
/// デバイスは途中で抜き差し・既定切り替えされうるため、毎回取得し直す。
/// </summary>
internal static class SystemAudio
{
    private const int eRender = 0;
    private const int eMultimedia = 1;
    private const int DEVICE_STATE_ACTIVE = 0x1;
    private const int STGM_READ = 0;
    private const int CLSCTX_ALL = 0x17;
    private const ushort VT_LPWSTR = 31;

    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    // PKEY_Device_FriendlyName: サウンド設定に表示される「ヘッドホン (HiBy FC4)」のような名前
    private static PROPERTYKEY PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        pid = 14,
    };

    public static void SetMute(bool mute, IReadOnlyList<string> deviceNames)
    {
        IMMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

            if (deviceNames.Count == 0)
            {
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out IMMDevice device));
                SetDeviceMute(device, mute);
                Marshal.ReleaseComObject(device);
                return;
            }

            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out IMMDeviceCollection devices));
            try
            {
                Marshal.ThrowExceptionForHR(devices.GetCount(out uint count));
                for (uint i = 0; i < count; i++)
                {
                    Marshal.ThrowExceptionForHR(devices.Item(i, out IMMDevice device));
                    try
                    {
                        string name = GetFriendlyName(device);
                        if (deviceNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                        {
                            SetDeviceMute(device, mute);
                        }
                    }
                    catch (COMException)
                    {
                        // 1台の失敗で他のデバイスの操作を止めない
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(devices);
            }
        }
        catch (COMException)
        {
            // 再生デバイスが1つも無い等。カジュアルツールなので黙って無視する。
        }
        finally
        {
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private static void SetDeviceMute(IMMDevice device, bool mute)
    {
        Guid iid = IID_IAudioEndpointVolume;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object obj));
        var volume = (IAudioEndpointVolume)obj;
        try
        {
            Marshal.ThrowExceptionForHR(volume.SetMute(mute, Guid.Empty));
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    private static string GetFriendlyName(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(STGM_READ, out IPropertyStore store));
        try
        {
            Marshal.ThrowExceptionForHR(store.GetValue(ref PKEY_Device_FriendlyName, out PROPVARIANT value));
            try
            {
                return value.vt == VT_LPWSTR ? Marshal.PtrToStringUni(value.pointerValue) ?? string.Empty : string.Empty;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // 文字列値の取得にしか使わないため、先頭の型タグとポインタ部分だけ定義する(x64で24バイト)
    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        private ushort reserved1;
        private ushort reserved2;
        private ushort reserved3;
        public IntPtr pointerValue;
        private IntPtr padding;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        // 以降のメソッドは使わないため宣言しない(vtable 末尾なので省略可能)
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        // SetMute までの vtable 順序を合わせるため、使わないメソッドもシグネチャを並べる
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDB, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDB, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);
    }
}
