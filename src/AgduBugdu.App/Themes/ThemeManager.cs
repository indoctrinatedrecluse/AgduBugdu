using System;
using Avalonia;
using Avalonia.Media;

namespace AgduBugdu.App.Themes;

public enum AppThemeMode
{
    LonelyDark,
    SolarizedContrast
}

public class ThemeDefinition
{
    public AppThemeMode Mode { get; set; }
    public string Name { get; set; } = string.Empty;
    public Color WindowBackground { get; set; }
    public Color SurfaceBackground { get; set; }
    public Color HeaderBackground { get; set; }
    public Color EditorBackground { get; set; }
    public Color EditorForeground { get; set; }
    public Color AccentColor { get; set; }
    public Color BorderBrush { get; set; }
    public Color StatusBarBackground { get; set; }
    public Color StatusBarForeground { get; set; }
    public Color TerminalBackground { get; set; }
    public Color TerminalForeground { get; set; }
    public Color SearchBarBackground { get; set; }
    public Color SelectionColor { get; set; }
}

public static class ThemeManager
{
    public static readonly ThemeDefinition LonelyDark = new()
    {
        Mode = AppThemeMode.LonelyDark,
        Name = "Lonely Dark",
        WindowBackground = Color.Parse("#121214"),
        SurfaceBackground = Color.Parse("#18181B"),
        HeaderBackground = Color.Parse("#1F1F23"),
        EditorBackground = Color.Parse("#141417"),
        EditorForeground = Color.Parse("#E4E4E7"),
        AccentColor = Color.Parse("#8B5CF6"), // Neon violet accent
        BorderBrush = Color.Parse("#27272A"),
        StatusBarBackground = Color.Parse("#6D28D9"),
        StatusBarForeground = Color.Parse("#FFFFFF"),
        TerminalBackground = Color.Parse("#09090B"),
        TerminalForeground = Color.Parse("#A1A1AA"),
        SearchBarBackground = Color.Parse("#18181B"),
        SelectionColor = Color.Parse("#3B1D82")
    };

    public static readonly ThemeDefinition SolarizedContrast = new()
    {
        Mode = AppThemeMode.SolarizedContrast,
        Name = "Solarized Contrast",
        WindowBackground = Color.Parse("#00212B"), // Deep rich obsidian teal
        SurfaceBackground = Color.Parse("#002B36"), // Base03
        HeaderBackground = Color.Parse("#073642"), // Base02
        EditorBackground = Color.Parse("#001E26"), // High-contrast deep cyan-black
        EditorForeground = Color.Parse("#93A1A1"), // Base1
        AccentColor = Color.Parse("#2AA198"), // Solarized Cyan
        BorderBrush = Color.Parse("#073642"),
        StatusBarBackground = Color.Parse("#2AA198"), // Cyan status bar
        StatusBarForeground = Color.Parse("#002B36"),
        TerminalBackground = Color.Parse("#00181F"),
        TerminalForeground = Color.Parse("#839496"),
        SearchBarBackground = Color.Parse("#073642"),
        SelectionColor = Color.Parse("#073642")
    };

    public static event EventHandler<ThemeDefinition>? ThemeChanged;

    public static ThemeDefinition CurrentTheme { get; private set; } = LonelyDark;

    public static void ApplyTheme(AppThemeMode mode)
    {
        CurrentTheme = mode == AppThemeMode.SolarizedContrast ? SolarizedContrast : LonelyDark;
        ThemeChanged?.Invoke(null, CurrentTheme);
    }
}
