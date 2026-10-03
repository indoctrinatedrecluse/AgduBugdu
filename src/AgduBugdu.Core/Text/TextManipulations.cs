using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AgduBugdu.Core.Text;

/// <summary>
/// Pure algorithmic manipulation operations on lines and text buffers.
/// </summary>
public static class TextManipulations
{
    /// <summary>
    /// Toggles line comments across a given collection of lines.
    /// If all non-empty lines are commented with <paramref name="commentPrefix"/>, comments are stripped.
    /// Otherwise, <paramref name="commentPrefix"/> is inserted at the common indentation.
    /// </summary>
    public static (List<string> Lines, bool WasCommented) ToggleLineComments(
        IReadOnlyList<string> lines,
        string commentPrefix)
    {
        if (lines == null || lines.Count == 0)
            return (new List<string>(), false);

        if (string.IsNullOrEmpty(commentPrefix))
            commentPrefix = "//";

        // Check if all non-empty lines are commented
        bool allCommented = true;
        bool hasNonEmptyLine = false;
        int minIndent = int.MaxValue;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            hasNonEmptyLine = true;
            int indent = GetLeadingWhitespaceCount(line);
            if (indent < minIndent)
                minIndent = indent;

            string trimmedStart = line.TrimStart();
            if (!trimmedStart.StartsWith(commentPrefix, StringComparison.Ordinal))
            {
                allCommented = false;
            }
        }

        if (!hasNonEmptyLine)
        {
            // All lines are blank/empty: comment them directly
            allCommented = false;
            minIndent = 0;
        }

        var result = new List<string>(lines.Count);

        if (allCommented)
        {
            // Uncomment: Remove prefix and optional space after prefix
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                    continue;
                }

                int indent = GetLeadingWhitespaceCount(line);
                string whitespace = line.Substring(0, indent);
                string rest = line.Substring(indent);

                if (rest.StartsWith(commentPrefix, StringComparison.Ordinal))
                {
                    rest = rest.Substring(commentPrefix.Length);
                    if (rest.StartsWith(" ", StringComparison.Ordinal))
                    {
                        rest = rest.Substring(1);
                    }
                }

                result.Add(whitespace + rest);
            }

            return (result, false);
        }
        else
        {
            // Comment: Insert prefix at minimum indentation
            if (minIndent == int.MaxValue) minIndent = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                    continue;
                }

                int indent = Math.Min(minIndent, line.Length);
                string before = line.Substring(0, indent);
                string after = line.Substring(indent);

                result.Add(before + commentPrefix + " " + after);
            }

            return (result, true);
        }
    }

    /// <summary>
    /// Toggles block comments around the provided text block.
    /// </summary>
    public static (string Text, bool WasCommented) ToggleBlockComment(
        string text,
        string blockStart,
        string blockEnd)
    {
        if (string.IsNullOrEmpty(blockStart)) blockStart = "/*";
        if (string.IsNullOrEmpty(blockEnd)) blockEnd = "*/";

        var trimmed = text.Trim();
        if (trimmed.StartsWith(blockStart, StringComparison.Ordinal) &&
            trimmed.EndsWith(blockEnd, StringComparison.Ordinal) &&
            trimmed.Length >= blockStart.Length + blockEnd.Length)
        {
            // Uncomment
            int startIdx = text.IndexOf(blockStart, StringComparison.Ordinal);
            int endIdx = text.LastIndexOf(blockEnd, StringComparison.Ordinal);

            if (startIdx >= 0 && endIdx >= startIdx + blockStart.Length)
            {
                var before = text.Substring(0, startIdx);
                var inner = text.Substring(startIdx + blockStart.Length, endIdx - (startIdx + blockStart.Length));
                var after = text.Substring(endIdx + blockEnd.Length);

                if (inner.StartsWith(" ", StringComparison.Ordinal))
                    inner = inner.Substring(1);
                if (inner.EndsWith(" ", StringComparison.Ordinal))
                    inner = inner.Substring(0, inner.Length - 1);

                return (before + inner + after, false);
            }
        }

        // Comment
        return ($"{blockStart} {text} {blockEnd}", true);
    }

    /// <summary>
    /// Shifts the specified range of lines up by one position.
    /// </summary>
    public static (List<string> Lines, int NewStartLineIndex) MoveLinesUp(
        IReadOnlyList<string> lines,
        int startLineIndex,
        int count)
    {
        var list = new List<string>(lines);
        if (startLineIndex <= 0 || count <= 0 || startLineIndex + count > list.Count)
            return (list, startLineIndex);

        var previousLine = list[startLineIndex - 1];
        list.RemoveAt(startLineIndex - 1);
        list.Insert(startLineIndex + count - 1, previousLine);

        return (list, startLineIndex - 1);
    }

    /// <summary>
    /// Shifts the specified range of lines down by one position.
    /// </summary>
    public static (List<string> Lines, int NewStartLineIndex) MoveLinesDown(
        IReadOnlyList<string> lines,
        int startLineIndex,
        int count)
    {
        var list = new List<string>(lines);
        if (count <= 0 || startLineIndex < 0 || startLineIndex + count >= list.Count)
            return (list, startLineIndex);

        var nextLine = list[startLineIndex + count];
        list.RemoveAt(startLineIndex + count);
        list.Insert(startLineIndex, nextLine);

        return (list, startLineIndex + 1);
    }

    /// <summary>
    /// Duplicates the specified range of lines above or below.
    /// </summary>
    public static (List<string> Lines, int InsertedAtIndex) DuplicateLines(
        IReadOnlyList<string> lines,
        int startLineIndex,
        int count,
        bool duplicateBelow)
    {
        var list = new List<string>(lines);
        if (count <= 0 || startLineIndex < 0 || startLineIndex + count > list.Count)
            return (list, startLineIndex);

        var slice = list.Skip(startLineIndex).Take(count).ToList();
        int insertIndex = duplicateBelow ? startLineIndex + count : startLineIndex;
        list.InsertRange(insertIndex, slice);

        return (list, insertIndex);
    }

    /// <summary>
    /// Deletes the specified range of lines.
    /// </summary>
    public static List<string> DeleteLines(
        IReadOnlyList<string> lines,
        int startLineIndex,
        int count)
    {
        var list = new List<string>(lines);
        if (count <= 0 || startLineIndex < 0 || startLineIndex >= list.Count)
            return list;

        int safeCount = Math.Min(count, list.Count - startLineIndex);
        list.RemoveRange(startLineIndex, safeCount);

        if (list.Count == 0)
        {
            list.Add(string.Empty);
        }

        return list;
    }

    /// <summary>
    /// Joins consecutive lines, trimming leading indentation from joined lines and separating with a single space.
    /// </summary>
    public static string JoinLines(IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (i == 0)
            {
                sb.Append(line.TrimEnd());
            }
            else
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]))
                    {
                        sb.Append(' ');
                    }
                    sb.Append(trimmed);
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Joins two lines with whitespace collapsing.
    /// </summary>
    public static string JoinTwoLines(string line1, string line2)
    {
        return JoinLines(new[] { line1, line2 });
    }

    /// <summary>
    /// Finds word boundaries (letters, digits, underscores) around the given offset.
    /// </summary>
    public static (int Start, int Length) FindWordBoundaries(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset < 0 || offset > text.Length)
            return (0, 0);

        if (offset == text.Length && offset > 0)
            offset--;

        if (!IsWordChar(text[offset]))
        {
            // If caret is right after a word character, check preceding
            if (offset > 0 && IsWordChar(text[offset - 1]))
            {
                offset--;
            }
            else
            {
                return (offset, 0);
            }
        }

        int start = offset;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        int end = offset;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }

        return (start, end - start);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int GetLeadingWhitespaceCount(string s)
    {
        int count = 0;
        while (count < s.Length && char.IsWhiteSpace(s[count]))
        {
            count++;
        }
        return count;
    }
}
