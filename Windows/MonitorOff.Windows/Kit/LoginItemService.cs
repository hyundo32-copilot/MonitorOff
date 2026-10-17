using Microsoft.Win32;

namespace MonitorOff.Kit;

public interface ILoginItemBackend
{
    bool IsRegistered { get; }

    void Register();

    void Unregister();
}

/// <summary>HKCU\Software\Microsoft\Windows\CurrentVersion\Run 으로 자동 시작을 등록한다.</summary>
public sealed class RegistryLoginItemBackend : ILoginItemBackend
{
    public const string ValueName = "MonitorOff";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _command;

    public RegistryLoginItemBackend(string? command = null)
    {
        _command = command ?? BuildCommand();
    }

    public static string BuildCommand()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("실행 파일 경로를 알 수 없습니다.");
        return $"\"{executable}\"";
    }

    public bool IsRegistered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    public void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, _command);
    }

    public void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

/// <summary>Sources/MonitorOffKit/LoginItemService.swift 의 포트.</summary>
public sealed class LoginItemService
{
    private readonly ILoginItemBackend _backend;

    public LoginItemService(ILoginItemBackend? backend = null)
    {
        _backend = backend ?? new RegistryLoginItemBackend();
    }

    public bool IsEnabled => _backend.IsRegistered;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            _backend.Register();
        }
        else
        {
            _backend.Unregister();
        }
    }
}
