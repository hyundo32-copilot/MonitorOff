using System.Runtime.InteropServices;

namespace MonitorOff;

/// <summary>
/// 숨은 메시지 전용 창으로 GUID_MONITOR_POWER_ON 전원 설정 통지를 받는다.
/// MonitorOff 가 끈 경우와 절전 타이머가 끈 경우 모두 같은 경로로 들어온다.
/// 콜백 인자: true = 화면 켜짐, false = 화면 꺼짐. UI 스레드에서 호출된다.
/// </summary>
public sealed class DisplayPowerNotifier : NativeWindow, IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtPowerSettingChange = 0x8013;
    private const uint DeviceNotifyWindowHandle = 0x00000000;
    private static readonly IntPtr HwndMessage = new(-3);

    // GUID_MONITOR_POWER_ON (winnt.h): 02731015-4510-4526-99E6-E5A17EBD1AEA
    private static readonly Guid GuidMonitorPowerOn = new("02731015-4510-4526-99E6-E5A17EBD1AEA");

    // POWERBROADCAST_SETTING { GUID PowerSetting; DWORD DataLength; UCHAR Data[1]; } → Data 오프셋 = 16 + 4
    private const int DataOffset = 20;

    private readonly Action<bool> _onChange;
    private IntPtr _registration;

    public DisplayPowerNotifier(Action<bool> onChange)
    {
        _onChange = onChange;
        CreateHandle(new CreateParams { Parent = HwndMessage });
        var guid = GuidMonitorPowerOn;
        _registration = RegisterPowerSettingNotification(Handle, ref guid, DeviceNotifyWindowHandle);
        // 등록 실패 시 통지가 오지 않을 뿐이므로 조용히 넘어간다 (음소거 기능만 동작 안 함).
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPowerBroadcast && (int)m.WParam == PbtPowerSettingChange && m.LParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<Guid>(m.LParam);
            if (setting == GuidMonitorPowerOn && Marshal.ReadInt32(m.LParam, 16) >= 1)
            {
                _onChange(Marshal.ReadByte(m.LParam, DataOffset) != 0);
            }
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registration != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_registration);
            _registration = IntPtr.Zero;
        }

        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid powerSettingGuid, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
}
