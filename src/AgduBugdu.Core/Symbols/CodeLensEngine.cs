using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AgduBugdu.Core.Symbols;

public class CodeLensItem
{
    public int Line { get; set; } // 1-indexed
    public int Column { get; set; } // 1-indexed
    public string SymbolName { get; set; } = string.Empty;
    public SymbolKind Kind { get; set; }
    public int ReferenceCount { get; set; }
    public bool IsTest { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string? AuthorTag { get; set; }
}

public static class CodeLensEngine
{
    // Matches methods, classes, interfaces, records, properties
    private static readonly Regex SymbolDefRegex = new(
        @"(?:(?:public|private|protected|internal|static|async|virtual|override|abstract|sealed)\s+)*(?:(?:class|interface|struct|record|enum)\s+(?<type>\w+)|(?<ret>[\w\<\>,\?]+(?:\s*\[\s*\])?)\s+(?<method>\w+)\s*\((?<params>[^\)]*)\)|(?<propType>[\w\<\>,\?]+(?:\s*\[\s*\])?)\s+(?<prop>\w+)\s*\{\s*(?:get|set|init))",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex TestAttributeRegex = new(
        @"\[\s*(?:Fact|Test|Theory|TestMethod)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Computes CodeLens entries for a document's source text.
    /// </summary>
    public static List<CodeLensItem> ResolveCodeLenses(string text, string? fileExtension = null)
    {
        var result = new List<CodeLensItem>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var lineOffsets = BuildLineOffsets(text);

        var matches = SymbolDefRegex.Matches(text);
        foreach (Match m in matches)
        {
            string name = string.Empty;
            SymbolKind kind = SymbolKind.Method;

            if (m.Groups["type"].Success)
            {
                name = m.Groups["type"].Value;
                kind = SymbolKind.Class;
            }
            else if (m.Groups["method"].Success)
            {
                name = m.Groups["method"].Value;
                kind = SymbolKind.Method;
            }
            else if (m.Groups["prop"].Success)
            {
                name = m.Groups["prop"].Value;
                kind = SymbolKind.Property;
            }

            if (string.IsNullOrEmpty(name) || name is "if" or "for" or "while" or "switch" or "catch" or "using" or "lock")
                continue;

            int lineIndex = GetLineFromOffset(lineOffsets, m.Index);
            if (lineIndex < 1 || lineIndex > lines.Length) continue;

            // Check if preceded by test attribute or if method name implies test
            bool isTest = false;
            if (kind == SymbolKind.Method)
            {
                // Check previous 3 lines for test attribute
                for (int back = Math.Max(0, lineIndex - 3); back < lineIndex; back++)
                {
                    if (TestAttributeRegex.IsMatch(lines[back]))
                    {
                        isTest = true;
                        break;
                    }
                }
                if (!isTest && (name.StartsWith("Test", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Test", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)))
                {
                    isTest = true;
                }
            }

            int refCount = CountReferences(text, name);

            string refText = refCount == 1 ? "1 reference" : $"{refCount} references";
            string displayText = isTest ? $"{refText} | ▶ Run Test" : refText;

            result.Add(new CodeLensItem
            {
                Line = lineIndex,
                Column = 1,
                SymbolName = name,
                Kind = kind,
                ReferenceCount = refCount,
                IsTest = isTest,
                DisplayText = displayText,
                AuthorTag = "You, edited recently"
            });
        }

        return result;
    }

    /// <summary>
    /// Counts occurrences of the identifier as a whole word, excluding the single definition site.
    /// </summary>
    public static int CountReferences(string text, string identifier)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(identifier)) return 0;
        try
        {
            var pattern = $@"\b{Regex.Escape(identifier)}\b";
            var matches = Regex.Matches(text, pattern);
            return Math.Max(0, matches.Count - 1);
        }
        catch
        {
            return 0;
        }
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
}
