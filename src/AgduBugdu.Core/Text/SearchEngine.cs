using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AgduBugdu.Core.Text;

public record SearchResult(int Offset, int Length, string Value);

public record SearchOptions(
    string Query,
    bool MatchCase = false,
    bool WholeWord = false,
    bool UseRegex = false
);

public static class SearchEngine
{
    public static IReadOnlyList<SearchResult> FindAll(string text, SearchOptions options)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.Query))
            return Array.Empty<SearchResult>();

        var results = new List<SearchResult>();

        try
        {
            if (options.UseRegex)
            {
                var regexOpts = RegexOptions.Multiline;
                if (!options.MatchCase)
                    regexOpts |= RegexOptions.IgnoreCase;

                string pattern = options.Query;
                if (options.WholeWord)
                    pattern = $@"\b(?:{pattern})\b";

                var matches = Regex.Matches(text, pattern, regexOpts);
                foreach (Match match in matches)
                {
                    if (match.Success && match.Length > 0)
                    {
                        results.Add(new SearchResult(match.Index, match.Length, match.Value));
                    }
                }
            }
            else
            {
                var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int queryLen = options.Query.Length;
                int start = 0;

                while (start <= text.Length - queryLen)
                {
                    int index = text.IndexOf(options.Query, start, comparison);
                    if (index == -1)
                        break;

                    if (options.WholeWord)
                    {
                        bool leftValid = index == 0 || !char.IsLetterOrDigit(text[index - 1]) && text[index - 1] != '_';
                        int rightIndex = index + queryLen;
                        bool rightValid = rightIndex >= text.Length || !char.IsLetterOrDigit(text[rightIndex]) && text[rightIndex] != '_';

                        if (leftValid && rightValid)
                        {
                            results.Add(new SearchResult(index, queryLen, text.Substring(index, queryLen)));
                        }
                    }
                    else
                    {
                        results.Add(new SearchResult(index, queryLen, text.Substring(index, queryLen)));
                    }

                    start = index + Math.Max(1, queryLen);
                }
            }
        }
        catch
        {
            // Invalid regular expressions fail gracefully without crashing
            return Array.Empty<SearchResult>();
        }

        return results;
    }

    public static (SearchResult Result, int Index, int TotalCount)? FindNext(string text, SearchOptions options, int currentOffset)
    {
        var matches = FindAll(text, options);
        if (matches.Count == 0)
            return null;

        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i].Offset >= currentOffset)
            {
                return (matches[i], i, matches.Count);
            }
        }

        // Wrap around to first match
        return (matches[0], 0, matches.Count);
    }

    public static (SearchResult Result, int Index, int TotalCount)? FindPrevious(string text, SearchOptions options, int currentOffset)
    {
        var matches = FindAll(text, options);
        if (matches.Count == 0)
            return null;

        for (int i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Offset < currentOffset)
            {
                return (matches[i], i, matches.Count);
            }
        }

        // Wrap around to last match
        return (matches[^1], matches.Count - 1, matches.Count);
    }

    public static (string NewText, int ReplacementCount) ReplaceAll(string text, SearchOptions options, string replacement)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.Query))
            return (text, 0);

        var matches = FindAll(text, options);
        if (matches.Count == 0)
            return (text, 0);

        // Replace from end to start to avoid shifting offsets
        var sb = new System.Text.StringBuilder(text);
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var m = matches[i];
            sb.Remove(m.Offset, m.Length);
            sb.Insert(m.Offset, replacement ?? string.Empty);
        }

        return (sb.ToString(), matches.Count);
    }
}
