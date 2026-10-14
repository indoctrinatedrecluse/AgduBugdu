using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AgduBugdu.Core.Symbols;

public enum BreadcrumbKind
{
    Folder,
    File,
    Symbol
}

public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "•";
    public string IconColor { get; set; } = "#AAAAAA";
    public BreadcrumbKind Kind { get; set; }
    public int LineNumber { get; set; } = 1;
    public int Column { get; set; } = 1;
    public bool IsLast { get; set; }
    public CodeSymbol? Symbol { get; set; }
}

public class StickyScopeInfo
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "•";
    public string IconColor { get; set; } = "#3B82F6";
    public int LineNumber { get; set; } = 1;
    public CodeSymbol? Symbol { get; set; }
}

public static class BreadcrumbResolver
{
    public static List<BreadcrumbItem> ResolveBreadcrumbs(string filePath, string content, int currentLine)
    {
        var items = new List<BreadcrumbItem>();

        // 1. Path hierarchy
        if (string.IsNullOrWhiteSpace(filePath))
        {
            items.Add(new BreadcrumbItem
            {
                Name = "Untitled",
                Icon = "📄",
                IconColor = "#60A5FA",
                Kind = BreadcrumbKind.File,
                LineNumber = 1
            });
        }
        else
        {
            string normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 0)
            {
                // Take up to 3 parent directories
                int dirCount = Math.Max(0, segments.Length - 1);
                int takeDirs = Math.Min(dirCount, 3);
                int skipDirs = dirCount - takeDirs;

                for (int i = skipDirs; i < dirCount; i++)
                {
                    items.Add(new BreadcrumbItem
                    {
                        Name = segments[i],
                        Icon = "📁",
                        IconColor = "#9CA3AF",
                        Kind = BreadcrumbKind.Folder,
                        LineNumber = 1
                    });
                }

                // File item
                items.Add(new BreadcrumbItem
                {
                    Name = segments[^1],
                    Icon = "📄",
                    IconColor = "#60A5FA",
                    Kind = BreadcrumbKind.File,
                    LineNumber = 1
                });
            }
        }

        // 2. Symbol hierarchy
        if (!string.IsNullOrWhiteSpace(content) && currentLine >= 1)
        {
            var symbols = CodeSymbolParser.ParseSymbols(content, filePath);
            var eligible = symbols.Where(s => s.LineNumber <= currentLine).OrderBy(s => s.LineNumber).ToList();

            if (eligible.Count > 0)
            {
                // Find highest enclosing type (Class, Interface, Struct, Record, Enum)
                var enclosingType = eligible.LastOrDefault(s =>
                    s.Kind == SymbolKind.Class ||
                    s.Kind == SymbolKind.Interface ||
                    s.Kind == SymbolKind.Struct ||
                    s.Kind == SymbolKind.Record ||
                    s.Kind == SymbolKind.Enum);

                if (enclosingType != null)
                {
                    items.Add(new BreadcrumbItem
                    {
                        Name = enclosingType.Name,
                        Icon = enclosingType.KindBadge,
                        IconColor = enclosingType.KindBadgeColor,
                        Kind = BreadcrumbKind.Symbol,
                        LineNumber = enclosingType.LineNumber,
                        Symbol = enclosingType
                    });
                }

                // Find enclosing member (Method, Function, Constructor, Property, Field, Heading)
                int minLine = enclosingType?.LineNumber ?? 0;
                var enclosingMember = eligible.LastOrDefault(s =>
                    s.LineNumber >= minLine &&
                    (s.Kind == SymbolKind.Method ||
                     s.Kind == SymbolKind.Function ||
                     s.Kind == SymbolKind.Constructor ||
                     s.Kind == SymbolKind.Property ||
                     s.Kind == SymbolKind.Heading));

                if (enclosingMember != null && enclosingMember != enclosingType)
                {
                    items.Add(new BreadcrumbItem
                    {
                        Name = enclosingMember.DisplayText,
                        Icon = enclosingMember.KindBadge,
                        IconColor = enclosingMember.KindBadgeColor,
                        Kind = BreadcrumbKind.Symbol,
                        LineNumber = enclosingMember.LineNumber,
                        Symbol = enclosingMember
                    });
                }
            }
        }

        // Set IsLast flag
        if (items.Count > 0)
        {
            items[^1].IsLast = true;
        }

        return items;
    }

    public static StickyScopeInfo? ResolveStickyScope(string filePath, string content, int topVisibleLine)
    {
        if (topVisibleLine <= 1 || string.IsNullOrWhiteSpace(content))
            return null;

        var symbols = CodeSymbolParser.ParseSymbols(content, filePath);
        var eligible = symbols.Where(s => s.LineNumber < topVisibleLine).OrderBy(s => s.LineNumber).ToList();

        if (eligible.Count == 0)
            return null;

        // Take closest symbol preceding the top visible line
        var target = eligible.Last();

        string title = target.Kind switch
        {
            SymbolKind.Class or SymbolKind.Interface or SymbolKind.Struct or SymbolKind.Record or SymbolKind.Enum => target.Name,
            _ => target.DisplayText
        };

        return new StickyScopeInfo
        {
            Title = title,
            Icon = target.KindBadge,
            IconColor = target.KindBadgeColor,
            LineNumber = target.LineNumber,
            Symbol = target
        };
    }
}
