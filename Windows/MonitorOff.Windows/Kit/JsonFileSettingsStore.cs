using System.Text.Json;

namespace MonitorOff.Kit;

/// <summary>%LOCALAPPDATA%\MonitorOff\settings.json 에 설정을 저장한다.</summary>
public sealed class JsonFileSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly Dictionary<string, string> _values;

    public JsonFileSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonitorOff",
            "settings.json");
        _values = Load();
    }

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string value)
    {
        _values[key] = value;
        Save();
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path))
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_path, JsonSerializer.Serialize(_values, options));
        }
        catch (Exception)
        {
            // 설정 저장 실패는 치명적이지 않으므로 무시한다.
        }
    }
}
