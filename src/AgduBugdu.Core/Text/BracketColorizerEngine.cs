using System;
using System.Collections.Generic;
using System.Linq;

namespace AgduBugdu.Core.Text;

public enum BracketType
{
    Round,   // ( )
    Curly,   // { }
    Square   // [ ]
}

public class RainbowBracketPair
{
    public int OpenOffset { get; set; }
    public int CloseOffset { get; set; }
    public int OpenLine { get; set; }
    public int CloseLine { get; set; }
    public int Depth { get; set; }
    public BracketType Type { get; set; }
    public string ColorHex { get; set; } = "#FFD700";
}

public class IndentationGuide
{
    public int Column { get; set; } // Indent column offset (e.g. 4, 8, 12)
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public bool IsActiveScope { get; set; }
}

public static class BracketColorizerEngine
{
    public static readonly string[] RainbowPalette = new[]
    {
        "#FACC15", // Gold / Yellow
        "#F472B6", // Pink / Magenta
        "#38BDF8", // Cyan / Blue
        "#4ADE80", // Green / Mint
        "#A78BFA"  // Purple / Violet
    };

    /// <summary>
    /// Analyzes the text and discovers all matching bracket pairs with assigned depth and rainbow color.
    /// </summary>
    public static List<RainbowBracketPair> FindRainbowBrackets(string text)
    {
        var pairs = new List<RainbowBracketPair>();
        if (string.IsNullOrEmpty(text))
            return pairs;

        var lineOffsets = BuildLineOffsets(text);
        var stack = new Stack<(char charType, int offset, int depth)>();

        bool inString = false;
        char stringChar = '\0';
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

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
                    i++;
                }
                else if (c == stringChar)
                {
                    inString = false;
                }
                continue;
            }

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

            // Open brackets
            if (c is '(' or '{' or '[')
            {
                int depth = stack.Count;
                stack.Push((c, i, depth));
            }
            // Close brackets
            else if (c is ')' or '}' or ']')
            {
                if (stack.Count > 0)
                {
                    var (openChar, openOffset, depth) = stack.Peek();
                    if (IsMatchingPair(openChar, c))
                    {
                        stack.Pop();
                        int openLine = GetLineFromOffset(lineOffsets, openOffset);
                        int closeLine = GetLineFromOffset(lineOffsets, i);

                        pairs.Add(new RainbowBracketPair
                        {
                            OpenOffset = openOffset,
                            CloseOffset = i,
                            OpenLine = openLine,
                            CloseLine = closeLine,
                            Depth = depth,
                            Type = GetBracketType(openChar),
                            ColorHex = RainbowPalette[depth % RainbowPalette.Length]
                        });
                    }
                }
            }
        }

        pairs.Sort((a, b) => a.OpenOffset.CompareTo(b.OpenOffset));
        return pairs;
    }

    /// <summary>
    /// Computes vertical indentation guide segments across all lines, marking the innermost block containing caretLine as active.
    /// </summary>
    public static List<IndentationGuide> ComputeIndentationGuides(string text, int tabSize = 4, int caretLine = 1)
    {
        var guides = new List<IndentationGuide>();
        if (string.IsNullOrEmpty(text))
            return guides;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var indentStack = new Stack<(int col, int startLine)>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int lineIndent = GetLineIndent(line, tabSize);
            int currentLine = i + 1;

            while (indentStack.Count > 0 && indentStack.Peek().col > lineIndent)
            {
                var (col, startLine) = indentStack.Pop();
                int endLine = currentLine - 1;
                if (endLine > startLine)
                {
                    guides.Add(new IndentationGuide
                    {
                        Column = col,
                        StartLine = startLine,
                        EndLine = endLine,
                        IsActiveScope = (caretLine >= startLine && caretLine <= endLine)
                    });
                }
            }

            for (int col = tabSize; col <= lineIndent; col += tabSize)
            {
                if (!indentStack.Any(x => x.col == col))
                {
                    indentStack.Push((col, currentLine));
                }
            }
        }

        while (indentStack.Count > 0)
        {
            var (col, startLine) = indentStack.Pop();
            int endLine = lines.Length;
            if (endLine > startLine)
            {
                guides.Add(new IndentationGuide
                {
                    Column = col,
                    StartLine = startLine,
                    EndLine = endLine,
                    IsActiveScope = (caretLine >= startLine && caretLine <= endLine)
                });
            }
        }

        return guides;
    }

    public static int GetLineIndent(string line, int tabSize)
    {
        int col = 0;
        foreach (char c in line)
        {
            if (c == ' ') col++;
            else if (c == '\t') col += tabSize;
            else break;
        }
        return col;
    }

    private static bool IsMatchingPair(char open, char close) =>
        (open == '(' && close == ')') ||
        (open == '{' && close == '}') ||
        (open == '[' && close == ']');

    private static BracketType GetBracketType(char c) => c switch
    {
        '(' or ')' => BracketType.Round,
        '{' or '}' => BracketType.Curly,
        '[' or ']' => BracketType.Square,
        _ => BracketType.Round
    };

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

    private static int GetLineFromOffset(List<int> lineOffsets, int offset)
    {
        int low = 0;
        int high = lineOffsets.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (lineOffsets[mid] <= offset)
            {
                if (mid == lineOffsets.Count - 1 || lineOffsets[mid + 1] > offset)
                    return mid + 1;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return 1;
    }
}
