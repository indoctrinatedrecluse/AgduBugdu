using System;
using System.IO;
using System.Text.Json;
using AgduBugdu.Core.Settings;

namespace AgduBugdu.Infrastructure.Settings;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private AppSettings _currentSettings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings CurrentSettings => _currentSettings;

    public SettingsService(string? customFilePath = null)
    {
        if (!string.IsNullOrEmpty(customFilePath))
        {
            _settingsFilePath = customFilePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "AgduBugdu");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        _currentSettings = LoadSettings();
    }

    public AppSettings LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _currentSettings = new AppSettings();
                    return _currentSettings;
                }

                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (loaded != null)
                {
                    SanitizeSettings(loaded);
                    _currentSettings = loaded;
                    return _currentSettings;
                }
            }
            catch
            {
                // Fall back to default on error
            }

            _currentSettings = new AppSettings();
            return _currentSettings;
        }
    }

    public void SaveSettings(AppSettings? settings = null)
    {
        lock (_lock)
        {
            try
            {
                if (settings != null)
                {
                    SanitizeSettings(settings);
                    _currentSettings = settings;
                }

                _currentSettings.LastModifiedUtc = DateTime.UtcNow;

                var dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception)
            {
                // Silently swallow or log
            }
        }

        SettingsChanged?.Invoke(this, _currentSettings);
    }

    public void ResetToDefaults()
    {
        var defaults = new AppSettings();
        SaveSettings(defaults);
    }

    private static void SanitizeSettings(AppSettings settings)
    {
        // Clamp Editor settings
        if (settings.Editor.FontSize < 8.0) settings.Editor.FontSize = 8.0;
        if (settings.Editor.FontSize > 48.0) settings.Editor.FontSize = 48.0;

        if (settings.Editor.TabSize < 1) settings.Editor.TabSize = 1;
        if (settings.Editor.TabSize > 16) settings.Editor.TabSize = 16;

        if (string.IsNullOrWhiteSpace(settings.Editor.FontFamily))
            settings.Editor.FontFamily = "Cascadia Code, Consolas, Courier New, monospace";

        // Clamp Git settings
        if (settings.Git.AutoFetchIntervalMinutes < 0) settings.Git.AutoFetchIntervalMinutes = 0;
        if (settings.Git.AutoFetchIntervalMinutes > 1440) settings.Git.AutoFetchIntervalMinutes = 1440;

        if (string.IsNullOrWhiteSpace(settings.Git.DefaultBranchName))
            settings.Git.DefaultBranchName = "master";

        // Terminal
        if (string.IsNullOrWhiteSpace(settings.Terminal.ShellProgram))
            settings.Terminal.ShellProgram = OperatingSystem.IsWindows() ? "powershell.exe" : "bash";

        // Theme
        if (string.IsNullOrWhiteSpace(settings.Theme.AccentColorHex))
            settings.Theme.AccentColorHex = "#8B5CF6";
    }
}
