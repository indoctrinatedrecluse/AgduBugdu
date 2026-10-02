using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgduBugdu.Plugin.DataGridLive;

public class CsvTableData
{
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
    public char Delimiter { get; }

    public CsvTableData(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, char delimiter)
    {
        Headers = headers;
        Rows = rows;
        Delimiter = delimiter;
    }
}

public static class CsvParser
{
    public static char DetectDelimiter(string sample)
    {
        if (string.IsNullOrEmpty(sample)) return ',';

        var commaCount = 0;
        var tabCount = 0;
        var semiCount = 0;
        var pipeCount = 0;

        using var reader = new StringReader(sample);
        for (int i = 0; i < 5; i++)
        {
            var line = reader.ReadLine();
            if (line == null) break;
            foreach (var ch in line)
            {
                if (ch == ',') commaCount++;
                else if (ch == '\t') tabCount++;
                else if (ch == ';') semiCount++;
                else if (ch == '|') pipeCount++;
            }
        }

        if (tabCount > commaCount && tabCount > semiCount && tabCount > pipeCount) return '\t';
        if (semiCount > commaCount && semiCount > tabCount && semiCount > pipeCount) return ';';
        if (pipeCount > commaCount && pipeCount > tabCount && pipeCount > semiCount) return '|';
        return ',';
    }

    public static CsvTableData Parse(string content, char? delimiter = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new CsvTableData(Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>(), ',');
        }

        char delim = delimiter ?? DetectDelimiter(content);
        var rawRows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // Skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delim)
                {
                    currentRow.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    // Ignore CR, handled at LF
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString().Trim());
                    currentField.Clear();
                    if (currentRow.Count > 0 && !(currentRow.Count == 1 && string.IsNullOrEmpty(currentRow[0])))
                    {
                        rawRows.Add(currentRow);
                    }
                    currentRow = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString().Trim());
            if (currentRow.Count > 0 && !(currentRow.Count == 1 && string.IsNullOrEmpty(currentRow[0])))
            {
                rawRows.Add(currentRow);
            }
        }

        if (rawRows.Count == 0)
        {
            return new CsvTableData(Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>(), delim);
        }

        var headers = rawRows[0];
        var dataRows = new List<IReadOnlyList<string>>();
        for (int r = 1; r < rawRows.Count; r++)
        {
            var row = rawRows[r];
            while (row.Count < headers.Count)
            {
                row.Add(string.Empty);
            }
            dataRows.Add(row);
        }

        return new CsvTableData(headers, dataRows, delim);
    }
}
