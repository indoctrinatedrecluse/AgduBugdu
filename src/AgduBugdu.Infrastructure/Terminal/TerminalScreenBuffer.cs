using System;
using System.Collections.Generic;
using System.Text;

namespace AgduBugdu.Infrastructure.Terminal;

/// <summary>
/// A lightweight VT100 / ANSI terminal emulator built on a fixed-size character grid.
///
/// It tracks cursor position, handles cursor movement and positioning sequences
/// (CSI nA/B/C/D, CSI r;cH), screen/line erasing (CSI 2J, CSI K), the alternate-screen
/// buffer (CSI ?1049h/l, CSI ?47h/l, CSI ?1047h/l), and carriage-return / line-feed
/// semantics. This is what allows full-screen TUI programs (nano, vim, htop, and custom
/// tools such as "ir nettop", "ir pmon", "ir edit") to redraw in place instead of
/// appending text, while normal shell output is preserved in scrollback.
/// </summary>
public class TerminalScreenBuffer
{
    private const int DefaultColumns = 200;
    private const int DefaultRows = 60;

    private readonly object _lock = new();

    private char[,] _grid;
    private int _row;
    private int _col;

    private int _savedRow;
    private int _savedCol;

    private bool _inAltBuffer;
    private char[,]? _savedGrid;
    private int _savedAltRow;
    private int _savedAltCol;
    private List<string>? _savedScrollback;

    private List<string> _scrollback = new();

    private readonly StringBuilder _pending = new();
    private bool _tuiDirty;

    public bool InAltBuffer => _inAltBuffer;
    public int Columns { get; }
    public int Rows { get; }
    public int ScrollbackLimit { get; set; } = 3000;

    public event EventHandler<bool>? TuiStateChanged;

    public TerminalScreenBuffer(int columns = DefaultColumns, int rows = DefaultRows)
    {
        Columns = Math.Max(20, columns);
        Rows = Math.Max(5, rows);
        _grid = CreateGrid();
    }

    private char[,] CreateGrid()
    {
        var g = new char[Rows, Columns];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                g[r, c] = ' ';
        return g;
    }

    public void Append(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        bool tuiChanged = false;
        bool newTuiState = false;

        lock (_lock)
        {
            var input = _pending.Length > 0 ? _pending.ToString() + text : text;
            _pending.Clear();

            int i = 0;
            while (i < input.Length)
            {
                char c = input[i];
                if (c == '\x1b')
                {
                    int next = ParseEscape(input, i);
                    if (next < 0)
                    {
                        _pending.Append(input, i, input.Length - i);
                        break;
                    }
                    i = next;
                }
                else
                {
                    WriteChar(c);
                    i++;
                }
            }

            tuiChanged = _tuiDirty;
            newTuiState = _inAltBuffer;
            _tuiDirty = false;
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
            var sb = new StringBuilder();

            if (!_inAltBuffer && _scrollback.Count > 0)
            {
                foreach (var line in _scrollback)
                {
                    sb.Append(line);
                    sb.Append('\n');
                }
            }

            int lastRow = Rows - 1;
            while (lastRow >= 0 && RowIsEmpty(lastRow)) lastRow--;

            for (int r = 0; r <= lastRow; r++)
            {
                sb.Append(RowToString(r));
                if (r < lastRow) sb.Append('\n');
            }

            return sb.ToString();
        }
    }

    public void Clear()
    {
        bool wasTui;
        lock (_lock)
        {
            wasTui = _inAltBuffer;
            _grid = CreateGrid();
            _row = 0;
            _col = 0;
            _savedRow = 0;
            _savedCol = 0;
            _inAltBuffer = false;
            _savedGrid = null;
            _savedAltRow = 0;
            _savedAltCol = 0;
            _savedScrollback = null;
            _scrollback.Clear();
            _pending.Clear();
            _tuiDirty = false;
        }
        if (wasTui) TuiStateChanged?.Invoke(this, false);
    }

    // ------------------------------------------------------------------ parsing

    private int ParseEscape(string s, int i)
    {
        if (i + 1 >= s.Length) return -1;

        switch (s[i + 1])
        {
            case '[':
                return ParseCsi(s, i + 2);
            case ']':
                return ParseOsc(s, i + 2);
            case '7':
                SaveCursor();
                return i + 2;
            case '8':
                RestoreCursor();
                return i + 2;
            case 'M': // Reverse Index
                _row = Math.Max(0, _row - 1);
                return i + 2;
            case 'D': // Index (line feed)
                MoveDown();
                return i + 2;
            case '=':
            case '>':
                return i + 2; // keypad mode select
            case '(':
            case ')':
                return i + 3 <= s.Length ? i + 3 : -1; // charset designation
            default:
                return i + 2; // unknown escape: consume ESC + next char
        }
    }

    private int ParseCsi(string s, int i)
    {
        int j = i;
        while (j < s.Length && s[j] >= 0x30 && s[j] <= 0x3F) j++; // parameter bytes
        while (j < s.Length && s[j] >= 0x20 && s[j] <= 0x2F) j++; // intermediate bytes
        if (j >= s.Length) return -1;

        char final = s[j];
        if (final < 0x40 || final > 0x7E)
        {
            return j + 1; // malformed: consume what we read
        }

        string paramStr = s.Substring(i, j - i);
        HandleCsi(paramStr, final);
        return j + 1;
    }

    private int ParseOsc(string s, int i)
    {
        // OSC ends at BEL (0x07) or ST (ESC \).
        int j = i;
        while (j < s.Length)
        {
            if (s[j] == '\x07') return j + 1;
            if (s[j] == '\x1b' && j + 1 < s.Length && s[j + 1] == '\\') return j + 2;
            j++;
        }
        return -1; // incomplete OSC
    }

    private void HandleCsi(string paramStr, char final)
    {
        bool isPrivate = paramStr.Length > 0 &&
            (paramStr[0] == '?' || paramStr[0] == '>' || paramStr[0] == '=' || paramStr[0] == '<');
        string trimmed = paramStr.TrimStart('?', '>', '=', '<');
        var parts = trimmed.Split(';');

        switch (final)
        {
            case 'A': // cursor up
                MoveUp(GetInt(parts, 0, 1));
                break;
            case 'B': // cursor down
                MoveDown(GetInt(parts, 0, 1));
                break;
            case 'C': // cursor forward
                _col = Math.Min(Columns - 1, _col + GetInt(parts, 0, 1));
                break;
            case 'D': // cursor back
                _col = Math.Max(0, _col - GetInt(parts, 0, 1));
                break;
            case 'E': // cursor next line
                MoveDown(GetInt(parts, 0, 1));
                _col = 0;
                break;
            case 'F': // cursor previous line
                MoveUp(GetInt(parts, 0, 1));
                _col = 0;
                break;
            case 'G': // cursor horizontal absolute
                _col = Math.Clamp(GetInt(parts, 0, 1) - 1, 0, Columns - 1);
                break;
            case 'H':
            case 'f': // cursor position
                _row = Math.Clamp(GetInt(parts, 0, 1) - 1, 0, Rows - 1);
                _col = Math.Clamp(GetInt(parts, 1, 1) - 1, 0, Columns - 1);
                break;
            case 'J': // erase in display
                EraseDisplay(GetInt(parts, 0, 0));
                break;
            case 'K': // erase in line
                EraseLine(GetInt(parts, 0, 0));
                break;
            case 'm': // SGR (colors etc.) — ignored
                break;
            case 'h':
            case 'l': // set / reset mode
                if (isPrivate)
                {
                    foreach (var part in parts)
                    {
                        if (int.TryParse(part, out int mode))
                        {
                            if (mode == 1049 || mode == 47 || mode == 1047)
                            {
                                if (final == 'h') EnterAltBuffer();
                                else ExitAltBuffer();
                            }
                            // Cursor visibility (25), application cursor keys (1),
                            // bracketed paste (2004), focus events (1004) — ignored.
                        }
                    }
                }
                break;
            default:
                break; // scroll, insert/delete line/char, etc. — ignored
        }
    }

    // ------------------------------------------------------------------ actions

    private void WriteChar(char c)
    {
        switch (c)
        {
            case '\n':
                MoveDown();
                break;
            case '\r':
                _col = 0;
                break;
            case '\b':
                if (_col > 0) _col--;
                break;
            case '\t':
                _col = Math.Min(Columns - 1, ((_col / 8) + 1) * 8);
                break;
            case '\x0c': // form feed == clear screen + home
                ClearGrid(_grid);
                _row = 0;
                _col = 0;
                break;
            case '\x07': // bell — ignored
                break;
            default:
                if (c >= 0x20 && c != 0x7F)
                {
                    _grid[_row, _col] = c;
                    _col++;
                    if (_col >= Columns)
                    {
                        _col = 0;
                        MoveDown();
                    }
                }
                break;
        }
    }

    private void MoveDown() => MoveDown(1);

    private void MoveDown(int n)
    {
        while (n-- > 0)
        {
            if (_row < Rows - 1) _row++;
            else ScrollUp();
        }
    }

    private void MoveUp(int n)
    {
        _row = Math.Max(0, _row - n);
    }

    private void ScrollUp()
    {
        if (!_inAltBuffer)
        {
            _scrollback.Add(RowToString(0));
            if (_scrollback.Count > ScrollbackLimit)
            {
                _scrollback.RemoveAt(0);
            }
        }

        for (int r = 0; r < Rows - 1; r++)
            for (int c = 0; c < Columns; c++)
                _grid[r, c] = _grid[r + 1, c];

        for (int c = 0; c < Columns; c++)
            _grid[Rows - 1, c] = ' ';
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end
                for (int c = _col; c < Columns; c++) _grid[_row, c] = ' ';
                for (int r = _row + 1; r < Rows; r++)
                    for (int c = 0; c < Columns; c++)
                        _grid[r, c] = ' ';
                break;
            case 1: // start to cursor
                for (int c = 0; c <= _col; c++) _grid[_row, c] = ' ';
                for (int r = 0; r < _row; r++)
                    for (int c = 0; c < Columns; c++)
                        _grid[r, c] = ' ';
                break;
            default: // 2 / 3 — entire display
                ClearGrid(_grid);
                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end
                for (int c = _col; c < Columns; c++) _grid[_row, c] = ' ';
                break;
            case 1: // start to cursor
                for (int c = 0; c <= _col; c++) _grid[_row, c] = ' ';
                break;
            default: // 2 — entire line
                for (int c = 0; c < Columns; c++) _grid[_row, c] = ' ';
                break;
        }
    }

    private void SaveCursor()
    {
        _savedRow = _row;
        _savedCol = _col;
    }

    private void RestoreCursor()
    {
        _row = Math.Clamp(_savedRow, 0, Rows - 1);
        _col = Math.Clamp(_savedCol, 0, Columns - 1);
    }

    private void EnterAltBuffer()
    {
        if (_inAltBuffer) return;
        _savedGrid = _grid;
        _savedAltRow = _row;
        _savedAltCol = _col;
        _savedScrollback = new List<string>(_scrollback);
        _grid = CreateGrid();
        _scrollback.Clear();
        _row = 0;
        _col = 0;
        _inAltBuffer = true;
        _tuiDirty = true;
    }

    private void ExitAltBuffer()
    {
        if (!_inAltBuffer) return;
        _grid = _savedGrid ?? CreateGrid();
        _row = Math.Clamp(_savedAltRow, 0, Rows - 1);
        _col = Math.Clamp(_savedAltCol, 0, Columns - 1);
        _scrollback = _savedScrollback ?? new List<string>();
        _savedGrid = null;
        _savedScrollback = null;
        _inAltBuffer = false;
        _tuiDirty = true;
    }

    // ------------------------------------------------------------------ helpers

    private static int GetInt(string[] parts, int index, int def)
    {
        if (index < parts.Length && int.TryParse(parts[index], out int value)) return value;
        return def;
    }

    private static void ClearGrid(char[,] grid)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                grid[r, c] = ' ';
    }

    private bool RowIsEmpty(int r)
    {
        for (int c = 0; c < Columns; c++)
            if (_grid[r, c] != ' ')
                return false;
        return true;
    }

    private string RowToString(int r)
    {
        int last = Columns - 1;
        while (last >= 0 && _grid[r, last] == ' ') last--;
        var sb = new StringBuilder(last + 1);
        for (int c = 0; c <= last; c++) sb.Append(_grid[r, c]);
        return sb.ToString();
    }
}
