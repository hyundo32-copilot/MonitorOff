using System.Globalization;

namespace MonitorOff.Kit;

public enum AppLanguage
{
    System,
    Korean,
    English,
}

public enum ResolvedLanguage
{
    Korean,
    English,
}

public interface ISettingsStore
{
    string? Get(string key);

    void Set(string key, string value);
}

/// <summary>Sources/MonitorOffKit/LanguageService.swift 의 포트. UserDefaults 대신 JSON 파일을 사용한다.</summary>
public sealed class LanguageService
{
    public const string StorageKey = "appLanguage";

    private readonly ISettingsStore _store;
    private readonly IReadOnlyList<string> _preferredLanguages;

    public LanguageService(
        ISettingsStore? store = null,
        IReadOnlyList<string>? preferredLanguages = null)
    {
        _store = store ?? new JsonFileSettingsStore();
        _preferredLanguages = preferredLanguages ?? new[] { CultureInfo.CurrentUICulture.Name };
    }

    public AppLanguage Selection
    {
        get
        {
            var raw = _store.Get(StorageKey);
            return Enum.TryParse<AppLanguage>(raw, ignoreCase: true, out var language)
                ? language
                : AppLanguage.System;
        }
    }

    public void SetSelection(AppLanguage language) =>
        _store.Set(StorageKey, language.ToString().ToLowerInvariant());

    public ResolvedLanguage EffectiveLanguage()
    {
        switch (Selection)
        {
            case AppLanguage.Korean:
                return ResolvedLanguage.Korean;
            case AppLanguage.English:
                return ResolvedLanguage.English;
            default:
                var first = _preferredLanguages.Count > 0 ? _preferredLanguages[0] : null;
                return first is not null && first.StartsWith("ko", StringComparison.OrdinalIgnoreCase)
                    ? ResolvedLanguage.Korean
                    : ResolvedLanguage.English;
        }
    }
}
