using System;
using System.Collections.Generic;

namespace AgduBugdu.Core.Symbols;

public enum SymbolKind
{
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Method,
    Function,
    Constructor,
    Property,
    Field,
    Heading,
    Section,
    Key
}

public class CodeSymbol
{
    public string Name { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public SymbolKind Kind { get; set; }
    public int LineNumber { get; set; }
    public int Column { get; set; } = 1;
    public string? LanguageId { get; set; }

    public string KindBadge => Kind switch
    {
        SymbolKind.Class => "C",
        SymbolKind.Interface => "I",
        SymbolKind.Struct => "S",
        SymbolKind.Record => "R",
        SymbolKind.Enum => "E",
        SymbolKind.Method => "M",
        SymbolKind.Function => "F",
        SymbolKind.Constructor => "init",
        SymbolKind.Property => "P",
        SymbolKind.Field => "f",
        SymbolKind.Heading => "H",
        SymbolKind.Section => "§",
        SymbolKind.Key => "K",
        _ => "•"
    };

    public string KindBadgeColor => Kind switch
    {
        SymbolKind.Class => "#EAB308",      // Amber
        SymbolKind.Interface => "#38BDF8",  // Cyan
        SymbolKind.Struct => "#F97316",     // Orange
        SymbolKind.Record => "#EC4899",     // Pink
        SymbolKind.Enum => "#A855F7",       // Purple
        SymbolKind.Method => "#60A5FA",     // Light Blue
        SymbolKind.Function => "#3B82F6",   // Blue
        SymbolKind.Constructor => "#10B981",// Emerald
        SymbolKind.Property => "#34D399",   // Mint
        SymbolKind.Field => "#94A3B8",      // Slate
        SymbolKind.Heading => "#F43F5E",    // Rose
        _ => "#CBD5E1"
    };

    public string DisplayText => string.IsNullOrWhiteSpace(Signature) ? Name : $"{Name} {Signature}";
}
