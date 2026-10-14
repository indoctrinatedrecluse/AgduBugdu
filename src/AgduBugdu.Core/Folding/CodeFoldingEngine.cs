using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AgduBugdu.Core.Folding;

public enum FoldingType
{
    Braces,
    Region,
    Comment,
    Tag,
    Indent
}

public class CodeFoldSection
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string HeaderText { get; set; } = "{...}";
    public FoldingType Type { get; set; } = FoldingType.Braces;
    public bool IsDefaultFolded { get; set; }
}

public static class CodeFoldingEngine
{
    private static readonly Regex RegionStartRegex = new(@"^\s*(?:#|//\s*#)\s*region\b(?:\s+(.*))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RegionEndRegex = new(@"^\s*(?:#|//\s*#)\s*endregion\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Analyzes the text and discovers all valid folding ranges across braces, custom #regions, multi-line comments, and indent blocks.
    /// </summary>
    public static List<CodeFoldSection> FindFoldings(string text, string? fileExtension = null)
    {
        var foldings = new List<CodeFoldSection>();
        if (string.IsNullOrEmpty(text))
            return foldings;

        var lineOffsets = BuildLineOffsets(text);

        // 1. Regions
        FindRegionFoldings(text, lineOffsets, foldings);

        // 2. Multiline Comments
        FindCommentFoldings(text, lineOffsets, foldings);

        // 3. Braces for braced languages
        FindBraceFoldings(text, lineOffsets, foldings);

        // 4. Indent-based blocks for Python/YAML
        if (IsIndentBasedLanguage(fileExtension))
        {
            FindIndentFoldings(text, lineOffsets, foldings);
        }

        foldings.Sort((a, b) => a.StartOffset != b.StartOffset 
            ? a.StartOffset.CompareTo(b.StartOffset) 
            : b.EndOffset.CompareTo(a.EndOffset));

        return foldings;
    }

    private static bool IsIndentBasedLanguage(string? ext)
    {
        if (string.IsNullOrEmpty(ext)) return false;
        var lower = ext.TrimStart('.').ToLowerInvariant();
        return lower is "py" or "yaml" or "yml";
    }

    private static List<int> BuildLineOffsets(string text)
    {
        var offsets = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                offsets.Add(i + 1);
            }
        }
        return offsets;
    }

    public static int GetLineFromOffset(List<int> lineOffsets, int offset)
    {
        int low = 0;
        int high = lineOffsets.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (lineOffsets[mid] <= offset)
            {
                if (mid == lineOffsets.Count - 1 || lineOffsets[mid + 1] > offset)
                    return mid + 1; // 1-indexed
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return 1;
    }

    private static void FindRegionFoldings(string text, List<int> lineOffsets, List<CodeFoldSection> foldings)
    {
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var regionStack = new Stack<(int lineIndex, int startOffset, string name)>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            var startMatch = RegionStartRegex.Match(line);
            if (startMatch.Success)
            {
                string name = startMatch.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(name)) name = "Region";
                int offset = lineOffsets[i];
                regionStack.Push((i + 1, offset, name));
            }
            else if (RegionEndRegex.IsMatch(line) && regionStack.Count > 0)
            {
                var (startLine, startOffset, name) = regionStack.Pop();
                int endLine = i + 1;
                if (endLine > startLine)
                {
                    int endOffset = lineOffsets[i] + line.Length;
                    foldings.Add(new CodeFoldSection
                    {
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        StartLine = startLine,
                        EndLine = endLine,
                        HeaderText = $"[Region: {name}]",
                        Type = FoldingType.Region
                    });
                }
            }
        }
    }

    private static void FindCommentFoldings(string text, List<int> lineOffsets, List<CodeFoldSection> foldings)
    {
        int index = 0;
        while (index < text.Length)
        {
            int startC = text.IndexOf("/*", index, StringComparison.Ordinal);
            int startXml = text.IndexOf("<!--", index, StringComparison.Ordinal);

            if (startC == -1 && startXml == -1) break;

            bool isXml = startXml != -1 && (startC == -1 || startXml < startC);
            int start = isXml ? startXml : startC;
            string endPattern = isXml ? "-->" : "*/";

            int end = text.IndexOf(endPattern, start + (isXml ? 4 : 2), StringComparison.Ordinal);
            if (end == -1) break;

            int endOffset = end + endPattern.Length;
            int startLine = GetLineFromOffset(lineOffsets, start);
            int endLine = GetLineFromOffset(lineOffsets, endOffset);

            if (endLine > startLine)
            {
                foldings.Add(new CodeFoldSection
                {
                    StartOffset = start,
                    EndOffset = endOffset,
                    StartLine = startLine,
                    EndLine = endLine,
                    HeaderText = isXml ? "<!-- ... -->" : "/* ... */",
                    Type = FoldingType.Comment
                });
            }

            index = endOffset;
        }
    }

    private static void FindBraceFoldings(string text, List<int> lineOffsets, List<CodeFoldSection> foldings)
    {
        var stack = new Stack<int>();
        bool inString = false;
        char stringChar = '\0';
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            // Handle newline comment reset
            if (c == '\n')
            {
                inLineComment = false;
                continue;
            }

            if (inLineComment) continue;

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }

            if (inString)
            {
                if (c == '\\')
                {
                    i++; // skip escaped char
                }
                else if (c == stringChar)
                {
                    inString = false;
                }
                continue;
            }

            // Check entering comments/strings
            if (c == '/' && next == '/')
            {
                inLineComment = true;
                i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }
            if (c is '"' or '\'')
            {
                inString = true;
                stringChar = c;
                continue;
            }

            // Track braces
            if (c == '{')
            {
                stack.Push(i);
            }
            else if (c == '}' && stack.Count > 0)
            {
                int openOffset = stack.Pop();
                int startLine = GetLineFromOffset(lineOffsets, openOffset);
                int endLine = GetLineFromOffset(lineOffsets, i);

                if (endLine > startLine)
                {
                    foldings.Add(new CodeFoldSection
                    {
                        StartOffset = openOffset,
                        EndOffset = i + 1,
                        StartLine = startLine,
                        EndLine = endLine,
                        HeaderText = "{...}",
                        Type = FoldingType.Braces
                    });
                }
            }
        }
    }

    private static void FindIndentFoldings(string text, List<int> lineOffsets, List<CodeFoldSection> foldings)
    {
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var indentStack = new Stack<(int lineIndex, int indent, int startOffset)>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int currentIndent = GetIndentation(line);

            while (indentStack.Count > 0 && indentStack.Peek().indent >= currentIndent)
            {
                var popped = indentStack.Pop();
                int endLine = i;
                if (endLine > popped.lineIndex)
                {
                    int endOffset = lineOffsets[i - 1] + lines[i - 1].Length;
                    foldings.Add(new CodeFoldSection
                    {
                        StartOffset = popped.startOffset,
                        EndOffset = endOffset,
                        StartLine = popped.lineIndex,
                        EndLine = endLine,
                        HeaderText = "...",
                        Type = FoldingType.Indent
                    });
                }
            }

            if (line.TrimEnd().EndsWith(':'))
            {
                int startOffset = lineOffsets[i] + line.Length;
                indentStack.Push((i + 1, currentIndent + 1, startOffset));
            }
        }

        while (indentStack.Count > 0)
        {
            var popped = indentStack.Pop();
            int endLine = lines.Length;
            if (endLine > popped.lineIndex)
            {
                int endOffset = lineOffsets[lines.Length - 1] + lines[lines.Length - 1].Length;
                foldings.Add(new CodeFoldSection
                {
                    StartOffset = popped.startOffset,
                    EndOffset = endOffset,
                    StartLine = popped.lineIndex,
                    EndLine = endLine,
                    HeaderText = "...",
                    Type = FoldingType.Indent
                });
            }
        }
    }

    private static int GetIndentation(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else if (c == '\t') count += 4;
            else break;
        }
        return count;
    }
}
