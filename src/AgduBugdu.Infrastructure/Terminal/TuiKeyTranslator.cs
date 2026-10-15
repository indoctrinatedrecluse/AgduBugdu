using System;

namespace AgduBugdu.Infrastructure.Terminal;

public enum TuiSpecialKey
{
    None,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    Insert,
    Delete,
    Escape,
    Tab,
    Enter,
    Backspace
}

/// <summary>
/// Generalized framework for translating user input, keyboard interrupts,
/// and navigation keys to standard ANSI / VT100 / POSIX terminal sequences.
/// Avoids application-specific hardcoding: works universally across nano,
/// vim, htop, ir ntop, ir edit, ir pmon, and any interactive CLI/TUI tools.
/// </summary>
public static class TuiKeyTranslator
{
    /// <summary>
    /// Translates any ASCII letter or symbol with Ctrl modifier into its standard ASCII control code.
    /// Pure mathematical translation: Ctrl+A (1) through Ctrl+Z (26).
    /// </summary>
    public static string? TranslateControlKey(char keyChar)
    {
        char upper = char.ToUpperInvariant(keyChar);
        if (upper >= 'A' && upper <= 'Z')
        {
            return ((char)(upper - 'A' + 1)).ToString();
        }

        return upper switch
        {
            '@' or '2' => "\0",
            '[' or '3' => "\x1b",
            '\\' or '4' => "\x1c",
            ']' or '5' => "\x1d",
            '^' or '6' => "\x1e",
            '_' or '7' => "\x1f",
            '?' or '8' => "\x7f",
            _ => null
        };
    }

    /// <summary>
    /// Translates navigation and control keys into standard ANSI/VT100 terminal escape sequences.
    /// </summary>
    public static string? TranslateSpecialKey(TuiSpecialKey key)
    {
        return key switch
        {
            TuiSpecialKey.Up => "\x1b[A",
            TuiSpecialKey.Down => "\x1b[B",
            TuiSpecialKey.Right => "\x1b[C",
            TuiSpecialKey.Left => "\x1b[D",
            TuiSpecialKey.Home => "\x1b[H",
            TuiSpecialKey.End => "\x1b[F",
            TuiSpecialKey.PageUp => "\x1b[5~",
            TuiSpecialKey.PageDown => "\x1b[6~",
            TuiSpecialKey.Insert => "\x1b[2~",
            TuiSpecialKey.Delete => "\x1b[3~",
            TuiSpecialKey.Escape => "\x1b",
            TuiSpecialKey.Tab => "\t",
            TuiSpecialKey.Enter => "\r",
            TuiSpecialKey.Backspace => "\x08",
            _ => null
        };
    }

    /// <summary>
    /// Generalized translation dispatch from key string name and modifier flags.
    /// </summary>
    public static string? Translate(string keyName, bool ctrl, bool alt = false, bool shift = false)
    {
        if (ctrl && keyName.Length == 1)
        {
            var res = TranslateControlKey(keyName[0]);
            if (res != null) return res;
        }

        if (Enum.TryParse<TuiSpecialKey>(keyName, ignoreCase: true, out var special) && special != TuiSpecialKey.None)
        {
            return TranslateSpecialKey(special);
        }

        // Common key name aliases
        return keyName.ToLowerInvariant() switch
        {
            "up" or "uparrow" => "\x1b[A",
            "down" or "downarrow" => "\x1b[B",
            "right" or "rightarrow" => "\x1b[C",
            "left" or "leftarrow" => "\x1b[D",
            "esc" or "escape" => "\x1b",
            "return" or "enter" => "\r",
            "back" or "backspace" => "\x08",
            "tab" => "\t",
            "home" => "\x1b[H",
            "end" => "\x1b[F",
            "pageup" or "prior" => "\x1b[5~",
            "pagedown" or "next" => "\x1b[6~",
            "insert" => "\x1b[2~",
            "delete" => "\x1b[3~",
            _ => null
        };
    }
}
