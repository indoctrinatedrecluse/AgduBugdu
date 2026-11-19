using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AgduBugdu.Core.Symbols;

public static class CodeSymbolParser
{
    private static readonly Regex CSharpTypeRegex = new(
        @"^\s*(?:(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*(?:class|interface|struct|record|enum)\s+([A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex CSharpMethodRegex = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|async|new)\s+)+(?:[A-Za-z0-9_<>?,\[\]\s]+\s+)?([A-Za-z0-9_]+)\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex CSharpPropRegex = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override)\s+)+(?:[A-Za-z0-9_<>?,\[\]\s]+)\s+([A-Za-z0-9_]+)\s*\{\s*(?:get|set|init)",
        RegexOptions.Compiled);

    private static readonly Regex PythonClassRegex = new(
        @"^\s*class\s+([A-Za-z0-9_]+)(?:\s*\(([^)]*)\))?:",
        RegexOptions.Compiled);

    private static readonly Regex PythonDefRegex = new(
        @"^\s*(?:async\s+)?def\s+([A-Za-z0-9_]+)\s*\(([^)]*)\):",
        RegexOptions.Compiled);

    private static readonly Regex RustFnRegex = new(
        @"^\s*(?:pub(?:\([^)]+\))?\s+)?(?:async\s+)?(?:unsafe\s+)?fn\s+([A-Za-z0-9_]+)\s*(?:<[^>]+>)?\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex RustTypeRegex = new(
        @"^\s*(?:pub(?:\([^)]+\))?\s+)?(?:struct|enum|trait|union|type)\s+([A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex RustImplRegex = new(
        @"^\s*impl(?:\s*<[^>]+>)?\s+(?:([A-Za-z0-9_]+)\s+for\s+)?([A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex GoFuncRegex = new(
        @"^\s*func\s+(?:\((?:[A-Za-z0-9_*\s]+)\)\s+)?([A-Za-z0-9_]+)\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex GoTypeRegex = new(
        @"^\s*type\s+([A-Za-z0-9_]+)\s+(?:struct|interface)",
        RegexOptions.Compiled);

    private static readonly Regex JsTsFuncRegex = new(
        @"^\s*(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s+([A-Za-z0-9_]+)\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex JsTsClassRegex = new(
        @"^\s*(?:export\s+)?(?:default\s+)?(?:abstract\s+)?(?:class|interface|type)\s+([A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownHeadingRegex = new(
        @"^(#{1,6})\s+(.+)$",
        RegexOptions.Compiled);

    public static List<CodeSymbol> Parse(string filePath, string content) => ParseSymbols(content, filePath);

    public static List<CodeSymbol> ParseSymbols(string content, string filePath)
    {
        var symbols = new List<CodeSymbol>();
        if (string.IsNullOrWhiteSpace(content))
            return symbols;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        using var reader = new StringReader(content);
        string? line;
        int lineNum = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                continue;

            switch (ext)
            {
                case ".cs":
                case ".java":
                case ".cpp":
                case ".c":
                case ".h":
                case ".hpp":
                    ParseCSharpLikeLine(line, lineNum, symbols);
                    break;

                case ".py":
                    ParsePythonLine(line, lineNum, symbols);
                    break;

                case ".rs":
                    ParseRustLine(line, lineNum, symbols);
                    break;

                case ".go":
                    ParseGoLine(line, lineNum, symbols);
                    break;

                case ".js":
                case ".ts":
                case ".jsx":
                case ".tsx":
                    ParseJsTsLine(line, lineNum, symbols);
                    break;

                case ".md":
                case ".markdown":
                    ParseMarkdownLine(line, lineNum, symbols);
                    break;
            }
        }

        ComputeSymbolScopes(symbols, content, ext);
        return symbols;
    }

    public static void ComputeSymbolScopes(List<CodeSymbol> symbols, string content, string ext)
    {
        if (symbols.Count == 0 || string.IsNullOrWhiteSpace(content))
            return;

        var lines = content.Split('\n');
        int totalLines = lines.Length;

        switch (ext)
        {
            case ".py":
                for (int i = 0; i < symbols.Count; i++)
                {
                    var sym = symbols[i];
                    if (sym.LineNumber < 1 || sym.LineNumber > totalLines) continue;
                    int startIndent = GetIndent(lines[sym.LineNumber - 1]);
                    int endLine = totalLines;
                    for (int l = sym.LineNumber; l < totalLines; l++)
                    {
                        string line = lines[l];
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        int currentIndent = GetIndent(line);
                        if (currentIndent <= startIndent)
                        {
                            endLine = l; // 1-based previous line
                            break;
                        }
                    }
                    sym.EndLineNumber = endLine;
                }
                break;

            case ".md":
            case ".markdown":
                for (int i = 0; i < symbols.Count; i++)
                {
                    var sym = symbols[i];
                    int nextSymLine = (i + 1 < symbols.Count) ? symbols[i + 1].LineNumber - 1 : totalLines;
                    sym.EndLineNumber = Math.Max(sym.LineNumber, nextSymLine);
                }
                break;

            default: // C-style brace languages (.cs, .java, .cpp, .c, .h, .hpp, .rs, .go, .js, .ts, etc.)
                foreach (var sym in symbols)
                {
                    if (sym.LineNumber < 1 || sym.LineNumber > totalLines) continue;
                    int depth = 0;
                    bool foundOpenBrace = false;
                    int endLine = totalLines;

                    for (int l = sym.LineNumber - 1; l < totalLines; l++)
                    {
                        string line = lines[l];
                        for (int c = 0; c < line.Length; c++)
                        {
                            char ch = line[c];
                            if (ch == '{')
                            {
                                depth++;
                                foundOpenBrace = true;
                            }
                            else if (ch == '}')
                            {
                                if (depth > 0)
                                {
                                    depth--;
                                    if (depth == 0 && foundOpenBrace)
                                    {
                                        endLine = l + 1; // 1-based
                                        goto FoundEnd;
                                    }
                                }
                            }
                        }
                    }

                FoundEnd:
                    sym.EndLineNumber = endLine;
                }
                break;
        }
    }

    private static int GetIndent(string line)
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

    private static void ParseCSharpLikeLine(string line, int lineNum, List<CodeSymbol> symbols)
    {
        var mType = CSharpTypeRegex.Match(line);
        if (mType.Success)
        {
            string name = mType.Groups[1].Value;
            SymbolKind kind = line.Contains("interface ") ? SymbolKind.Interface
                : line.Contains("struct ") ? SymbolKind.Struct
                : line.Contains("record ") ? SymbolKind.Record
                : line.Contains("enum ") ? SymbolKind.Enum
                : SymbolKind.Class;

            symbols.Add(new CodeSymbol
            {
                Name = name,
                Kind = kind,
                LineNumber = lineNum,
                Signature = line.Trim()
            });
            return;
        }

        var mProp = CSharpPropRegex.Match(line);
        if (mProp.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mProp.Groups[1].Value,
                Kind = SymbolKind.Property,
                LineNumber = lineNum,
                Signature = "{ get; set; }"
            });
            return;
        }

        var mMethod = CSharpMethodRegex.Match(line);
        if (mMethod.Success)
        {
            string name = mMethod.Groups[1].Value;
            if (name == "if" || name == "for" || name == "foreach" || name == "while" || name == "switch" || name == "catch")
                return;

            symbols.Add(new CodeSymbol
            {
                Name = name,
                Kind = SymbolKind.Method,
                LineNumber = lineNum,
                Signature = $"({mMethod.Groups[2].Value.Trim()})"
            });
        }
    }

    private static void ParsePythonLine(string line, int lineNum, List<CodeSymbol> symbols)
    {
        var mClass = PythonClassRegex.Match(line);
        if (mClass.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mClass.Groups[1].Value,
                Kind = SymbolKind.Class,
                LineNumber = lineNum,
                Signature = mClass.Groups[2].Success ? $"({mClass.Groups[2].Value})" : ""
            });
            return;
        }

        var mDef = PythonDefRegex.Match(line);
        if (mDef.Success)
        {
            string name = mDef.Groups[1].Value;
            symbols.Add(new CodeSymbol
            {
                Name = name,
                Kind = name == "__init__" ? SymbolKind.Constructor : SymbolKind.Function,
                LineNumber = lineNum,
                Signature = $"({mDef.Groups[2].Value.Trim()})"
            });
        }
    }

    private static void ParseRustLine(string line, int lineNum, List<CodeSymbol> symbols)
    {
        var mType = RustTypeRegex.Match(line);
        if (mType.Success)
        {
            SymbolKind kind = line.Contains("enum ") ? SymbolKind.Enum
                : line.Contains("trait ") ? SymbolKind.Interface
                : SymbolKind.Struct;

            symbols.Add(new CodeSymbol
            {
                Name = mType.Groups[1].Value,
                Kind = kind,
                LineNumber = lineNum,
                Signature = line.Trim()
            });
            return;
        }

        var mImpl = RustImplRegex.Match(line);
        if (mImpl.Success)
        {
            string target = mImpl.Groups[2].Value;
            symbols.Add(new CodeSymbol
            {
                Name = $"impl {target}",
                Kind = SymbolKind.Class,
                LineNumber = lineNum,
                Signature = line.Trim()
            });
            return;
        }

        var mFn = RustFnRegex.Match(line);
        if (mFn.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mFn.Groups[1].Value,
                Kind = SymbolKind.Function,
                LineNumber = lineNum,
                Signature = $"({mFn.Groups[2].Value.Trim()})"
            });
        }
    }

    private static void ParseGoLine(string line, int lineNum, List<CodeSymbol> symbols)
    {
        var mType = GoTypeRegex.Match(line);
        if (mType.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mType.Groups[1].Value,
                Kind = line.Contains("interface") ? SymbolKind.Interface : SymbolKind.Struct,
                LineNumber = lineNum,
                Signature = line.Trim()
            });
            return;
        }

        var mFunc = GoFuncRegex.Match(line);
        if (mFunc.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mFunc.Groups[1].Value,
                Kind = SymbolKind.Function,
                LineNumber = lineNum,
                Signature = $"({mFunc.Groups[2].Value.Trim()})"
            });
        }
    }

    private static void ParseJsTsLine(string line, int lineNum, List<CodeSymbol> symbols)
    {
        var mClass = JsTsClassRegex.Match(line);
        if (mClass.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mClass.Groups[1].Value,
                Kind = line.Contains("interface ") ? SymbolKind.Interface : SymbolKind.Class,
                LineNumber = lineNum,
                Signature = line.Trim()
            });
            return;
        }

        var mFunc = JsTsFuncRegex.Match(line);
        if (mFunc.Success)
        {
            symbols.Add(new CodeSymbol
            {
                Name = mFunc.Groups[1].Value,
                Kind = SymbolKind.Function,
                LineNumber = lineNum,
                Signature = $"({mFunc.Groups[2].Value.Trim()})"
            });
        }
    }

    private static void ParseMarkdownLine(string line, int lineNum, List<CodeSymbol> symbols)
    {
        var mHead = MarkdownHeadingRegex.Match(line);
        if (mHead.Success)
        {
            int level = mHead.Groups[1].Value.Length;
            string title = mHead.Groups[2].Value.Trim();
            symbols.Add(new CodeSymbol
            {
                Name = title,
                Kind = SymbolKind.Heading,
                LineNumber = lineNum,
                Signature = $"H{level}"
            });
        }
    }
}
