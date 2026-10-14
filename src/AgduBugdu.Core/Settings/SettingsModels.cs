using System;
using System.Collections.Generic;

namespace AgduBugdu.Core.Settings;

public class EditorSettings
{
    public string FontFamily { get; set; } = "Cascadia Code, Consolas, Courier New, monospace";
    public double FontSize { get; set; } = 14.0;
    public double LineSpacing { get; set; } = 1.0;
    public int TabSize { get; set; } = 4;
    public bool InsertSpaces { get; set; } = true;
    public bool WordWrap { get; set; } = false;
    public bool ShowMinimap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public string CursorStyle { get; set; } = "Line"; // "Line", "Block", "Underline"

    public EditorSettings Clone() => new()
    {
        FontFamily = FontFamily,
        FontSize = FontSize,
        LineSpacing = LineSpacing,
        TabSize = TabSize,
        InsertSpaces = InsertSpaces,
        WordWrap = WordWrap,
        ShowMinimap = ShowMinimap,
        ShowLineNumbers = ShowLineNumbers,
        CursorStyle = CursorStyle
    };
}

public class TerminalSettings
{
    public string ShellProgram { get; set; } = OperatingSystem.IsWindows() ? "powershell.exe" : "bash";
    public string CustomArguments { get; set; } = OperatingSystem.IsWindows() ? "-NoLogo" : "";
    public string StartDirectory { get; set; } = "";

    public TerminalSettings Clone() => new()
    {
        ShellProgram = ShellProgram,
        CustomArguments = CustomArguments,
        StartDirectory = StartDirectory
    };
}

public class GitSettings
{
    public int AutoFetchIntervalMinutes { get; set; } = 0; // 0 = disabled
    public string DefaultBranchName { get; set; } = "master";
    public string DiffStyle { get; set; } = "Unified"; // "Unified", "SideBySide"

    public GitSettings Clone() => new()
    {
        AutoFetchIntervalMinutes = AutoFetchIntervalMinutes,
        DefaultBranchName = DefaultBranchName,
        DiffStyle = DiffStyle
    };
}

public class ThemeSettings
{
    public string ThemeMode { get; set; } = "LonelyDark"; // "LonelyDark", "SolarizedContrast"
    public string AccentColorHex { get; set; } = "#8B5CF6";

    public ThemeSettings Clone() => new()
    {
        ThemeMode = ThemeMode,
        AccentColorHex = AccentColorHex
    };
}

public class KeymapSettings
{
    public Dictionary<string, string> CustomBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public KeymapSettings Clone()
    {
        var clone = new KeymapSettings();
        foreach (var kvp in CustomBindings)
        {
            clone.CustomBindings[kvp.Key] = kvp.Value;
        }
        return clone;
    }
}

public class AppSettings
{
    public EditorSettings Editor { get; set; } = new();
    public TerminalSettings Terminal { get; set; } = new();
    public GitSettings Git { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public KeymapSettings Keymap { get; set; } = new();
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

    public AppSettings Clone() => new()
    {
        Editor = Editor.Clone(),
        Terminal = Terminal.Clone(),
        Git = Git.Clone(),
        Theme = Theme.Clone(),
        Keymap = Keymap.Clone(),
        LastModifiedUtc = LastModifiedUtc
    };
}

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    event EventHandler<AppSettings>? SettingsChanged;
    AppSettings LoadSettings();
    void SaveSettings(AppSettings? settings = null);
    void ResetToDefaults();
}
