using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgduBugdu.Plugin.HexViewer;

public class HexLine
{
    public long Offset { get; set; }
    public string OffsetHex => Offset.ToString("X8");
    public string HexFormatted { get; set; } = string.Empty;
    public string AsciiFormatted { get; set; } = string.Empty;
    public string FullDisplayLine => $"{OffsetHex}  {HexFormatted,-49}  |{AsciiFormatted}|";
}

public class HexViewerStats
{
    public long FileSizeBytes { get; set; }
    public double ShannonEntropy { get; set; }
    public double NullByteRatio { get; set; }
    public double PrintableAsciiRatio { get; set; }

    public string SummaryText =>
        $"Size: {FormatSize(FileSizeBytes)} | Entropy: {ShannonEntropy:F3}/8.0 | Null: {NullByteRatio * 100:F1}% | ASCII: {PrintableAsciiRatio * 100:F1}%";

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

public static class HexDumper
{
    public const int BytesPerLine = 16;

    public static List<HexLine> DumpBytes(byte[] buffer, long startOffset = 0)
    {
        var lines = new List<HexLine>((buffer.Length + BytesPerLine - 1) / BytesPerLine);

        for (int i = 0; i < buffer.Length; i += BytesPerLine)
        {
            int lineLen = Math.Min(BytesPerLine, buffer.Length - i);
            var hexSb = new StringBuilder(50);
            var asciiSb = new StringBuilder(lineLen);

            for (int b = 0; b < BytesPerLine; b++)
            {
                if (b == 8) hexSb.Append(' ');

                if (b < lineLen)
                {
                    byte val = buffer[i + b];
                    hexSb.Append(val.ToString("X2")).Append(' ');
                    char ch = (val >= 32 && val <= 126) ? (char)val : '.';
                    asciiSb.Append(ch);
                }
                else
                {
                    hexSb.Append("   ");
                }
            }

            lines.Add(new HexLine
            {
                Offset = startOffset + i,
                HexFormatted = hexSb.ToString().TrimEnd(),
                AsciiFormatted = asciiSb.ToString()
            });
        }

        return lines;
    }

    public static HexViewerStats CalculateStats(byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
            return new HexViewerStats();

        int[] frequency = new int[256];
        long nullCount = 0;
        long printableCount = 0;

        foreach (byte b in buffer)
        {
            frequency[b]++;
            if (b == 0) nullCount++;
            if (b >= 32 && b <= 126) printableCount++;
        }

        double entropy = 0.0;
        double len = buffer.Length;
        for (int i = 0; i < 256; i++)
        {
            if (frequency[i] > 0)
            {
                double p = frequency[i] / len;
                entropy -= p * Math.Log2(p);
            }
        }

        return new HexViewerStats
        {
            FileSizeBytes = buffer.Length,
            ShannonEntropy = entropy,
            NullByteRatio = nullCount / len,
            PrintableAsciiRatio = printableCount / len
        };
    }
}
