using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MonitorOff.Kit;

public interface ISystemAwakeService : IDisposable
{
    bool IsHeld { get; }

    void Acquire();

    void Release();
}

/// <summary>
/// 시스템이 대기모드에 빠지지 않도록 전원 단언을 유지한다.
///
/// SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED) 를 사용한다.
/// 단언은 호출한 스레드에 귀속되므로 반드시 앱을 돌리는 UI 스레드에서만
/// Acquire / Release 를 호출해야 한다.
///
/// ES_DISPLAY_REQUIRED 를 걸지 않으므로 디스플레이는 그대로 꺼질 수 있다.
/// PowerCreateRequest 도 후보였지만 이 시스템에서 전 파라미터 조합이
/// ERROR_INVALID_PARAMETER 로 거부되어 폐기했다.
/// </summary>
public sealed class SystemAwakeService : ISystemAwakeService
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private int _ownerThreadId;

    public bool IsHeld { get; private set; }

    public void Acquire()
    {
        if (IsHeld)
        {
            return;
        }

        // 이전 상태를 반환하며 0 이면 실패다.
        var previous = SetThreadExecutionState(EsContinuous | EsSystemRequired);
        if (previous == 0)
        {
            throw new SleepException(
                $"전원 단언을 설정할 수 없습니다. (Win32 오류 {Marshal.GetLastWin32Error()})");
        }

        _ownerThreadId = Environment.CurrentManagedThreadId;
        IsHeld = true;
    }

    public void Release()
    {
        if (!IsHeld)
        {
            return;
        }

        IsHeld = false;

        if (_ownerThreadId != Environment.CurrentManagedThreadId)
        {
            // 단언은 스레드에 묶여 있어 다른 스레드에서는 해제되지 않는다.
            Trace.WriteLine(
                $"MonitorOff: 전원 단언 해제가 다른 스레드에서 발생했습니다 ({_ownerThreadId} -> {Environment.CurrentManagedThreadId}).");
        }

        _ownerThreadId = 0;

        // ES_CONTINUOUS 만 전달하면 이 스레드의 단언이 해제된다.
        SetThreadExecutionState(EsContinuous);
    }

    public void Dispose() => Release();
}
