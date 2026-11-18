using System.Runtime.InteropServices;

namespace MonitorOff.Kit;

/// <summary>
/// Core Audio(MMDevice / EndpointVolume) COM interop 으로 기본 렌더링 엔드포인트의 음소거를 제어한다.
/// GUID·메서드 순서는 Windows SDK 헤더 mmdeviceapi.h / endpointvolume.h 기준이다.
/// 기본 장치는 바뀔 수 있으므로 호출마다 새로 조회한다. UI(STA) 스레드에서 호출한다.
/// </summary>
public sealed class SystemAudioMuter : IAudioMuter
{
    private const int ERender = 0;   // EDataFlow.eRender
    private const int EConsole = 0;  // ERole.eConsole
    private const uint ClsctxAll = 0x17;

    public bool IsMuted()
    {
        var volume = GetEndpointVolume();
        try
        {
            volume.GetMute(out var muted);
            return muted;
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    public void SetMuted(bool muted)
    {
        var volume = GetEndpointVolume();
        try
        {
            var context = Guid.Empty;
            volume.SetMute(muted, ref context);
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    private static IAudioEndpointVolume GetEndpointVolume()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        IMMDevice? device = null;
        try
        {
            enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var instance);
            return (IAudioEndpointVolume)instance;
        }
        finally
        {
            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            Marshal.ReleaseComObject(enumerator);
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices); // vtable 자리 채우기용

        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(
            ref Guid iid,
            uint clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    // endpointvolume.h 의 선언 순서를 그대로 따라야 vtable 이 맞는다. GetMute 뒤 메서드는 쓰지 않아 생략했다.
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);

        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
