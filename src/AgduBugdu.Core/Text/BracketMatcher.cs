using System;

namespace AgduBugdu.Core.Text;

public static class BracketMatcher
{
    private static readonly (char Open, char Close)[] Pairs =
    {
        ('(', ')'),
        ('[', ']'),
        ('{', '}'),
        ('<', '>')
    };

    /// <summary>
    /// Finds the matching bracket position for the bracket at or immediately preceding <paramref name="offset"/>.
    /// Returns the target offset of the matching bracket, or null if no matching bracket was found.
    /// </summary>
    public static int? FindMatchingBracket(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset < 0 || offset > text.Length)
            return null;

        // Check character under caret or character immediately before caret
        int targetPos = -1;
        if (offset < text.Length && IsBracket(text[offset]))
        {
            targetPos = offset;
        }
        else if (offset > 0 && IsBracket(text[offset - 1]))
        {
            targetPos = offset - 1;
        }

        if (targetPos < 0)
            return null;

        char current = text[targetPos];

        foreach (var (open, close) in Pairs)
        {
            if (current == open)
            {
                // Scan forward
                int depth = 1;
                for (int i = targetPos + 1; i < text.Length; i++)
                {
                    if (text[i] == open)
                    {
                        depth++;
                    }
                    else if (text[i] == close)
                    {
                        depth--;
                        if (depth == 0)
                            return i;
                    }
                }
                return null;
            }

            if (current == close)
            {
                // Scan backward
                int depth = 1;
                for (int i = targetPos - 1; i >= 0; i--)
                {
                    if (text[i] == close)
                    {
                        depth++;
                    }
                    else if (text[i] == open)
                    {
                        depth--;
                        if (depth == 0)
                            return i;
                    }
                }
                return null;
            }
        }

        return null;
    }

    private static bool IsBracket(char c)
    {
        return c is '(' or ')' or '[' or ']' or '{' or '}' or '<' or '>';
    }
}
