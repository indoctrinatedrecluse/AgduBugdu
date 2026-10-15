using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AgduBugdu.Infrastructure.Terminal;

/// <summary>
/// Screen buffer engine ported from IndoctrinatedEdit.
/// Automatically detects TUI alternate screen buffer mode sets (\x1b[?1049h, \x1b[?47h),
/// preserves normal scrollback buffer on TUI entry/exit, clears frames on \x1b[2J/\x1b[H,
/// handles carriage return line overwrites (\r), and cleans ANSI control codes.
/// </summary>
public class TerminalScreenBuffer
{
    private readonly List<string> _buffer = new();
    private List<string> _savedBuffer = new();
    private readonly object _lock = new();

    public bool InAltBuffer { get; private set; }
    public int ScrollbackLimit { get; set; } = 3000;

    public event EventHandler<bool>? TuiStateChanged;

    private static readonly Regex AltBufferEnterRegex = new(@"\x1b\[\?(?:1049|47|1047)h", RegexOptions.Compiled);
    private static readonly Regex AltBufferExitRegex = new(@"\x1b\[\?(?:1049|47|1047)l", RegexOptions.Compiled);
    private static readonly Regex ClearScreenRegex = new(@"\x1b\[[23]J", RegexOptions.Compiled);
    private static readonly Regex CursorHomeRegex = new(@"\x1b\[(?:\d+)?(?:;\d+)?[Hf]", RegexOptions.Compiled);
    private static readonly Regex CursorModeRegex = new(@"\x1b\[\?(?:25|1|2004|9001|1004)[lh]", RegexOptions.Compiled);
    private static readonly Regex WindowTitleRegex = new(@"\x1b\]0;[^\x07]+\x07", RegexOptions.Compiled);
    private static readonly Regex LineClearRegex = new(@"\x1b\[[012]?K", RegexOptions.Compiled);
    private static readonly Regex AnsiSgrRegex = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    private static readonly Regex DecKeypadRegex = new(@"\x1b[=>]|\x1b\([AB012]", RegexOptions.Compiled);

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        bool tuiChanged = false;
        bool newTuiState = InAltBuffer;

        lock (_lock)
        {
            // 1. Alternate Screen Buffer Switching (\x1b[?1049h / \x1b[?1049l or \x1b[?47h / \x1b[?47l)
            if (AltBufferEnterRegex.IsMatch(text))
            {
                if (!InAltBuffer)
                {
                    _savedBuffer = new List<string>(_buffer);
                    InAltBuffer = true;
                    _buffer.Clear();
                    tuiChanged = true;
                    newTuiState = true;
                }
                text = AltBufferEnterRegex.Replace(text, string.Empty);
            }

            if (AltBufferExitRegex.IsMatch(text))
            {
                if (InAltBuffer)
                {
                    _buffer.Clear();
                    _buffer.AddRange(_savedBuffer);
                    InAltBuffer = false;
                    tuiChanged = true;
                    newTuiState = false;
                }
                text = AltBufferExitRegex.Replace(text, string.Empty);
            }

            // 2. Full Screen Clear (\x1b[2J, \x1b[3J) in TUI mode
            if (InAltBuffer && ClearScreenRegex.IsMatch(text))
            {
                _buffer.Clear();
            }
            text = ClearScreenRegex.Replace(text, string.Empty);

            // 3. Cursor Home (\x1b[H, \x1b[1;1H) in TUI mode
            if (InAltBuffer && CursorHomeRegex.IsMatch(text))
            {
                if (text.StartsWith("\x1b[H") || text.StartsWith("\x1b[1;1H") || text.StartsWith("\x1b[?25l\x1b[H"))
                {
                    _buffer.Clear();
                }
            }
            text = CursorHomeRegex.Replace(text, string.Empty);

            // 4. Strip cursor visibility / mode sequences / window title / line clears / DEC keypad / ANSI SGR colors
            text = CursorModeRegex.Replace(text, string.Empty);
            text = WindowTitleRegex.Replace(text, string.Empty);
            text = LineClearRegex.Replace(text, string.Empty);
            text = DecKeypadRegex.Replace(text, string.Empty);
            text = AnsiSgrRegex.Replace(text, string.Empty);

            if (string.IsNullOrEmpty(text))
            {
                if (tuiChanged)
                {
                    TuiStateChanged?.Invoke(this, newTuiState);
                }
                return;
            }

            // 5. Split lines while handling carriage returns
            var chunks = Regex.Split(text, @"\r?\n");
            int startIdx = 0;

            if (_buffer.Count > 0 && !text.StartsWith("\n") && !text.StartsWith("\r\n"))
            {
                string first = chunks[0];
                if (first.Contains('\r'))
                {
                    var rParts = first.Split('\r');
                    _buffer[^1] = rParts[^1];
                }
                else
                {
                    _buffer[^1] += first;
                }
                startIdx = 1;
            }
            else if (_buffer.Count > 0 && (text.StartsWith("\n") || text.StartsWith("\r\n")))
            {
                startIdx = 1;
            }

            for (int i = startIdx; i < chunks.Length; i++)
            {
                string chunk = chunks[i];
                if (chunk.Contains('\r'))
                {
                    var rParts = chunk.Split('\r');
                    chunk = rParts[^1];
                }
                _buffer.Add(chunk);
            }

            if (_buffer.Count > ScrollbackLimit)
            {
                _buffer.RemoveRange(0, _buffer.Count - ScrollbackLimit);
            }
        }

        if (tuiChanged)
        {
            TuiStateChanged?.Invoke(this, newTuiState);
        }
    }

    public string GetText()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _buffer);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _savedBuffer.Clear();
        }
    }
}
