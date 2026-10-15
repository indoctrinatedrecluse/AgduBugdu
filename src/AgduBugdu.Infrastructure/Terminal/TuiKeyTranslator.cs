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

    private static readonly System.Collections.Generic.Dictionary<string, string> ActionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["up"] = "\x1b[A",
        ["down"] = "\x1b[B",
        ["right"] = "\x1b[C",
        ["left"] = "\x1b[D",
        ["arrowup"] = "\x1b[A",
        ["arrowdown"] = "\x1b[B",
        ["arrowleft"] = "\x1b[D",
        ["arrowright"] = "\x1b[C",
        ["enter"] = "\r",
        ["return"] = "\r",
        ["escape"] = "\x1b",
        ["esc"] = "\x1b",
        ["tab"] = "\t",
        ["backspace"] = "\x7f",
        ["bksp"] = "\x7f",
        ["bs"] = "\x7f",
        ["delete"] = "\x1b[3~",
        ["del"] = "\x1b[3~",
        ["insert"] = "\x1b[2~",
        ["ins"] = "\x1b[2~",
        ["space"] = " ",
        ["spacebar"] = " ",
        ["pageup"] = "\x1b[5~",
        ["pgup"] = "\x1b[5~",
        ["pagedown"] = "\x1b[6~",
        ["pgdn"] = "\x1b[6~",
        ["home"] = "\x1b[H",
        ["end"] = "\x1b[F",
        ["shift+tab"] = "\x1b[Z",
        ["backtab"] = "\x1b[Z",
        ["ctrl-c"] = "\x03",
        ["ctrl+c"] = "\x03",
        ["ctrl-d"] = "\x04",
        ["ctrl+d"] = "\x04",
        ["ctrl-z"] = "\x1a",
        ["ctrl+z"] = "\x1a",
        ["ctrl-l"] = "\x0c",
        ["ctrl+l"] = "\x0c",
        ["ctrl-x"] = "\x18",
        ["ctrl+x"] = "\x18",
        ["ctrl-o"] = "\x0f",
        ["ctrl+o"] = "\x0f",
        ["ctrl-s"] = "\x13",
        ["ctrl+s"] = "\x13",
        ["ctrl-w"] = "\x17",
        ["ctrl+w"] = "\x17",
        ["ctrl-k"] = "\x0b",
        ["ctrl+k"] = "\x0b",
        ["ctrl-u"] = "\x15",
        ["ctrl+u"] = "\x15",
        ["ctrl-a"] = "\x01",
        ["ctrl+a"] = "\x01",
        ["ctrl-e"] = "\x05",
        ["ctrl+e"] = "\x05",
        ["ctrl-g"] = "\x07",
        ["ctrl+g"] = "\x07",
        ["ctrl-r"] = "\x12",
        ["ctrl+r"] = "\x12",
        ["q"] = "q",
        ["w"] = "w",
        ["a"] = "a",
        ["s"] = "s",
        ["d"] = "d",
        ["h"] = "h",
        ["j"] = "j",
        ["k"] = "k",
        ["l"] = "l",
        ["r"] = "r",
        ["c"] = "c",
        ["x"] = "x",
        ["y"] = "y",
        ["n"] = "n",
        ["m"] = "m",
        ["p"] = "p"
    };

    /// <summary>
    /// Dispatches raw TUI key sequences (Arrows, Enter, Esc, Q, WASD, Ctrl Combos) directly to terminal.
    /// Ported from IndoctrinatedEdit sendTuiKey architecture.
    /// </summary>
    public static string ResolveTuiAction(string action)
    {
        if (string.IsNullOrEmpty(action)) return string.Empty;
        var normalized = action.Trim();
        if (ActionMap.TryGetValue(normalized, out var seq))
        {
            return seq;
        }

        // Generic Ctrl+<char> chords
        if ((normalized.StartsWith("ctrl+", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("ctrl-", StringComparison.OrdinalIgnoreCase)) && normalized.Length >= 6)
        {
            var charPart = normalized[5..];
            if (charPart.Length == 1)
            {
                var ctrlSeq = TranslateControlKey(charPart[0]);
                if (ctrlSeq != null) return ctrlSeq;
            }
        }

        return Translate(normalized, false) ?? action;
    }

    /// <summary>
    /// Generalized translation dispatch from key string name and modifier flags.
    /// </summary>
    public static string? Translate(string keyName, bool ctrl, bool alt = false, bool shift = false)
    {
        if (string.IsNullOrEmpty(keyName)) return null;

        if (ctrl && keyName.Length == 1)
        {
            var res = TranslateControlKey(keyName[0]);
            if (res != null) return res;
        }

        if (keyName.Length > 1 && !char.IsDigit(keyName[0]) && Enum.TryParse<TuiSpecialKey>(keyName, ignoreCase: true, out var special) && special != TuiSpecialKey.None)
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
            "back" or "backspace" => "\x7f",
            "tab" => "\t",
            "home" => "\x1b[H",
            "end" => "\x1b[F",
            "pageup" or "prior" => "\x1b[5~",
            "pagedown" or "next" => "\x1b[6~",
            "insert" => "\x1b[2~",
            "delete" => "\x1b[3~",
            _ => keyName.Length == 1 ? keyName : null
        };
    }
}
