using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AgduBugdu.Plugin.HexViewer;

public class HexViewerViewModel
{
    public const int PageSize = 4096; // 4KB per page (256 lines)

    private string? _loadedFilePath;
    private byte[] _pageBuffer = Array.Empty<byte>();
    private long _totalFileSize;
    private long _currentOffset;

    public string Title => "Hex / Binary Inspector";
    public string? LoadedFilePath => _loadedFilePath;
    public string FileName => string.IsNullOrEmpty(_loadedFilePath) ? "No File" : Path.GetFileName(_loadedFilePath);
    public long TotalFileSize => _totalFileSize;
    public long CurrentOffset => _currentOffset;
    public int TotalPages => (int)Math.Max(1, (_totalFileSize + PageSize - 1) / PageSize);
    public int CurrentPage => (int)(_currentOffset / PageSize) + 1;
    public string PageSummary => $"Page {CurrentPage} / {TotalPages} (Offset: 0x{_currentOffset:X8} - 0x{Math.Min(_totalFileSize, _currentOffset + _pageBuffer.Length):X8})";

    public List<HexLine> DisplayLines { get; private set; } = new();
    public HexViewerStats Stats { get; private set; } = new();
    public string FormattedDumpText { get; private set; } = string.Empty;
    public string SearchResultText { get; private set; } = string.Empty;

    public event EventHandler? FileLoaded;
    public event EventHandler? PageChanged;

    public void LoadFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        _loadedFilePath = filePath;
        var fileInfo = new FileInfo(filePath);
        _totalFileSize = fileInfo.Length;
        _currentOffset = 0;

        // Calculate sample stats from first 64KB (or whole file)
        int sampleLen = (int)Math.Min(_totalFileSize, 65536);
        byte[] sample = new byte[sampleLen];
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fs.ReadExactly(sample, 0, sampleLen);
        }
        Stats = HexDumper.CalculateStats(sample);
        Stats.FileSizeBytes = _totalFileSize;

        LoadCurrentPage();
        FileLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void LoadBytes(byte[] bytes, string name = "MemoryBuffer.bin")
    {
        _loadedFilePath = name;
        _totalFileSize = bytes.Length;
        _currentOffset = 0;
        Stats = HexDumper.CalculateStats(bytes);

        _pageBuffer = bytes.Take(PageSize).ToArray();
        DisplayLines = HexDumper.DumpBytes(_pageBuffer, 0);
        UpdateFormattedDump();
        FileLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void NextPage()
    {
        if (_currentOffset + PageSize < _totalFileSize)
        {
            _currentOffset += PageSize;
            LoadCurrentPage();
        }
    }

    public void PreviousPage()
    {
        if (_currentOffset - PageSize >= 0)
        {
            _currentOffset -= PageSize;
            LoadCurrentPage();
        }
        else if (_currentOffset > 0)
        {
            _currentOffset = 0;
            LoadCurrentPage();
        }
    }

    public void GoToOffset(long offset)
    {
        if (offset < 0) offset = 0;
        if (offset >= _totalFileSize) offset = Math.Max(0, _totalFileSize - 1);

        _currentOffset = (offset / HexDumper.BytesPerLine) * HexDumper.BytesPerLine;
        LoadCurrentPage();
    }

    public long? Search(string query, bool isHex = false)
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath) || !File.Exists(_loadedFilePath) || string.IsNullOrWhiteSpace(query))
            return null;

        byte[] pattern;
        if (isHex)
        {
            var cleaned = query.Replace(" ", "").Replace("-", "");
            if (cleaned.Length % 2 != 0) return null;
            pattern = Convert.FromHexString(cleaned);
        }
        else
        {
            pattern = Encoding.UTF8.GetBytes(query);
        }

        if (pattern.Length == 0) return null;

        using var fs = new FileStream(_loadedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] buffer = new byte[65536];
        long globalOffset = 0;
        int bytesRead;

        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i <= bytesRead - pattern.Length; i++)
            {
                bool match = true;
                for (int p = 0; p < pattern.Length; p++)
                {
                    if (buffer[i + p] != pattern[p])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    long foundOffset = globalOffset + i;
                    SearchResultText = $"Match found at offset 0x{foundOffset:X8} ({foundOffset})";
                    GoToOffset(foundOffset);
                    return foundOffset;
                }
            }

            globalOffset += bytesRead;
        }

        SearchResultText = $"No match found for '{query}'";
        return null;
    }

    private void LoadCurrentPage()
    {
        if (string.IsNullOrEmpty(_loadedFilePath) || !File.Exists(_loadedFilePath))
            return;

        int bytesToRead = (int)Math.Min(PageSize, _totalFileSize - _currentOffset);
        if (bytesToRead <= 0)
        {
            DisplayLines.Clear();
            FormattedDumpText = string.Empty;
            return;
        }

        _pageBuffer = new byte[bytesToRead];
        using (var fs = new FileStream(_loadedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fs.Seek(_currentOffset, SeekOrigin.Begin);
            fs.ReadExactly(_pageBuffer, 0, bytesToRead);
        }

        DisplayLines = HexDumper.DumpBytes(_pageBuffer, _currentOffset);
        UpdateFormattedDump();
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFormattedDump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Offset(h)  00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  Decoded text");
        sb.AppendLine($"--------  -----------------------------------------------  ----------------");
        foreach (var line in DisplayLines)
        {
            sb.AppendLine(line.FullDisplayLine);
        }
        FormattedDumpText = sb.ToString();
    }
}
