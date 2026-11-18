namespace MonitorOff.Kit;

public interface IAudioMuter
{
    bool IsMuted();

    void SetMuted(bool muted);
}

/// <summary>
/// Sources/MonitorOffKit/DisplayMuteController.swift 의 포트.
/// 화면이 꺼져 있는 동안 시스템 음소거를 거는 순수 상태 기계이며, 우리가 건 음소거만 되돌린다.
/// 오디오 호출 실패는 사용자에게 알리지 않고 삼킨다 (<paramref name="log"/> 로만 남긴다).
/// </summary>
public sealed class DisplayMuteController
{
    private readonly IAudioMuter _audio;
    private readonly Action<string> _log;

    public DisplayMuteController(IAudioMuter audio, bool enabled = true, Action<string>? log = null)
    {
        _audio = audio;
        Enabled = enabled;
        _log = log ?? (_ => { });
    }

    public bool Enabled { get; private set; }

    public bool MutedByUs { get; private set; }

    public void OnDisplaySleep()
    {
        if (!Enabled)
        {
            return;
        }

        // 이미 우리가 음소거해 둔 상태에서 중복 통지가 와도 그대로 둔다.
        if (MutedByUs)
        {
            return;
        }

        try
        {
            if (_audio.IsMuted())
            {
                return; // 이미 음소거 → 건드리지 않음
            }

            _audio.SetMuted(true);
            MutedByUs = true;
        }
        catch (Exception ex)
        {
            _log($"MonitorOff mute failed: {ex.Message}");
        }
    }

    public void OnDisplayWake() => RestoreIfMutedByUs();

    public void SetEnabled(bool value)
    {
        Enabled = value;
        if (!value)
        {
            RestoreIfMutedByUs();
        }
    }

    public void RestoreOnExit() => RestoreIfMutedByUs();

    private void RestoreIfMutedByUs()
    {
        if (!MutedByUs)
        {
            return;
        }

        try
        {
            _audio.SetMuted(false);
        }
        catch (Exception ex)
        {
            _log($"MonitorOff unmute failed: {ex.Message}");
        }

        MutedByUs = false;
    }
}
