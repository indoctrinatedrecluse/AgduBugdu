using Avalonia.Input;
using AgduBugdu.Infrastructure.Terminal;

namespace AgduBugdu.App.Views.Tools;

/// <summary>
/// Maps Avalonia <see cref="Key"/> / <see cref="KeyModifiers"/> input into the raw
/// bytes that should be written to a child shell's stdin for interactive TUI sessions.
/// Keeps the view free of ad-hoc key logic and is unit-testable without a GUI.
/// </summary>
public static class TerminalKeyMapper
{
    public static string? ToRawSequence(Key key, KeyModifiers modifiers)
    {
        bool ctrl = modifiers.HasFlag(KeyModifiers.Control);
        bool alt = modifiers.HasFlag(KeyModifiers.Alt);
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);

        // Alt-chords have no standard raw-TTY meaning; let them pass through untouched.
        if (alt) return null;

        // Ctrl+letter => ASCII control code (Ctrl+C = 0x03, Ctrl+X = 0x18, ...).
        if (ctrl && !shift && key >= Key.A && key <= Key.Z)
        {
            return TuiKeyTranslator.TranslateControlKey((char)('A' + (key - Key.A)));
        }

        // Non-printable navigation / editing keys.
        var special = key switch
        {
            Key.Up => "\x1b[A",
            Key.Down => "\x1b[B",
            Key.Right => "\x1b[C",
            Key.Left => "\x1b[D",
            Key.Home => "\x1b[H",
            Key.End => "\x1b[F",
            Key.PageUp => "\x1b[5~",
            Key.PageDown => "\x1b[6~",
            Key.Insert => "\x1b[2~",
            Key.Delete => "\x1b[3~",
            Key.Escape => "\x1b",
            Key.Tab => shift ? "\x1b[Z" : "\t",
            Key.Enter => "\r",
            Key.Back => "\x7f",
            Key.Space => " ",
            _ => null
        };
        if (special != null) return special;

        // Printable characters (US layout) — covers WASD navigation, C/Q interrupts, etc.
        var ch = ToPrintableChar(key, shift);
        return ch == null ? null : ch.ToString();
    }

    public static char? ToPrintableChar(Key key, bool shift)
    {
        return key switch
        {
            >= Key.A and <= Key.Z => shift ? (char)('A' + (key - Key.A)) : (char)('a' + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => shift ? ")!@#$%^&*("[(int)(key - Key.D0)] : (char)('0' + (key - Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
            Key.Space => ' ',
            Key.OemSemicolon => shift ? ':' : ';',
            Key.OemPlus => shift ? '+' : '=',
            Key.OemComma => shift ? '<' : ',',
            Key.OemMinus => shift ? '_' : '-',
            Key.OemPeriod => shift ? '>' : '.',
            Key.OemQuestion => shift ? '?' : '/',
            Key.OemTilde => shift ? '~' : '`',
            Key.OemOpenBrackets => shift ? '{' : '[',
            Key.OemPipe => shift ? '|' : '\\',
            Key.OemCloseBrackets => shift ? '}' : ']',
            Key.OemQuotes => shift ? '"' : '\'',
            Key.OemBackslash => shift ? '|' : '\\',
            _ => null
        };
    }
}
