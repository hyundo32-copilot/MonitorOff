namespace MonitorOff.Kit;

public enum L10nKey
{
    SleepNow,
    LaunchAtLogin,
    Quit,
    SleepFailedTitle,
    LoginFailedTitle,
    AwakeFailedTitle,
}

/// <summary>Sources/MonitorOffKit/L10n.swift 의 포트.</summary>
public static class L10n
{
    public static string String(L10nKey key, ResolvedLanguage language) => (key, language) switch
    {
        (L10nKey.SleepNow, ResolvedLanguage.Korean) => "지금 대기모드",
        (L10nKey.SleepNow, ResolvedLanguage.English) => "Sleep Display Now",
        (L10nKey.LaunchAtLogin, ResolvedLanguage.Korean) => "로그인 시 자동 실행",
        (L10nKey.LaunchAtLogin, ResolvedLanguage.English) => "Open at Login",
        (L10nKey.Quit, ResolvedLanguage.Korean) => "종료",
        (L10nKey.Quit, ResolvedLanguage.English) => "Quit",
        (L10nKey.SleepFailedTitle, ResolvedLanguage.Korean) => "대기모드 전환 실패",
        (L10nKey.SleepFailedTitle, ResolvedLanguage.English) => "Failed to Sleep Display",
        (L10nKey.LoginFailedTitle, ResolvedLanguage.Korean) => "자동 실행 설정 실패",
        (L10nKey.LoginFailedTitle, ResolvedLanguage.English) => "Failed to Set Login Item",
        (L10nKey.AwakeFailedTitle, ResolvedLanguage.Korean) => "시스템 대기 방지 설정 실패",
        (L10nKey.AwakeFailedTitle, ResolvedLanguage.English) => "Failed to Prevent System Sleep",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };
}
