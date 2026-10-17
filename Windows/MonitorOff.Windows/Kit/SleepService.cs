using System.Runtime.InteropServices;

namespace MonitorOff.Kit;

public sealed class SleepException : Exception
{
    public SleepException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 디스플레이만 끈다. 시스템 전원 상태에는 영향을 주지 않으며,
/// 대기모드 방지는 <see cref="SystemAwakeService"/> 가 담당한다.
/// </summary>
public sealed class SleepService
{
    public delegate void PowerOffAction();

    private readonly PowerOffAction _powerOff;

    public SleepService(PowerOffAction? powerOff = null)
    {
        _powerOff = powerOff ?? NativeDisplaySleep.TurnOffDisplay;
    }

    public void Sleep() => _powerOff();
}

/// <summary>
/// SC_MONITORPOWER(-1) 로 디스플레이를 즉시 끈다. 시스템/백그라운드 작업은 그대로 유지된다.
/// </summary>
public static class NativeDisplaySleep
{
    private const int HwndBroadcast = 0xFFFF;
    private const uint WmSysCommand = 0x0112;
    private const uint ScMonitorPower = 0xF170;
    private const uint SmtoAbortIfHung = 0x0002;

    /// <summary>-1 = 전원 끔, 1 = 대기, 2 = 절전, -1 로 디스플레이가 꺼진다.</summary>
    private static readonly IntPtr MonitorPowerOff = new(-1);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    public static void TurnOffDisplay()
    {
        // HWND_BROADCAST 가 통과하지 못하면 Progman(셸 데스크톱 창)에 직접 보낸다.
        if (!TrySend(HwndBroadcast))
        {
            var progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                TrySend(progman);
            }
        }
    }

    private static bool TrySend(IntPtr hWnd)
    {
        // 반환값 0 은 타임아웃 또는 처리한 창이 없음을 뜻한다.
        var result = SendMessageTimeout(
            hWnd, WmSysCommand, IntPtr.Zero, MonitorPowerOff, SmtoAbortIfHung, 2000, out _);
        return result != IntPtr.Zero;
    }
}
