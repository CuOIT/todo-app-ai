using System;
using System.IO;
using System.Text.Json;

namespace AiTaskTracker.Services;

public sealed class UiPreferencesStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public UiPreferencesStore(string dataDirectory)
    {
        _path = Path.Combine(dataDirectory, "ui-preferences.json");
    }

    public UiPreferences Load()
    {
        if (!File.Exists(_path))
        {
            return new UiPreferences();
        }

        try
        {
            return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(_path), _jsonOptions)
                   ?? new UiPreferences();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UiPreferences();
        }
    }

    public void Save(UiPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, _jsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}

public sealed class UiPreferences
{
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 820;
    public bool IsMaximized { get; set; }
    public bool IsAlwaysOnTop { get; set; }
    public int ViewModeIndex { get; set; }
}
