using System;
using System.Collections.Generic;
using System.IO;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Core.Text;

/// <summary>
/// Provides comment syntax definitions and lookup based on file extensions and language definitions.
/// </summary>
public static class CommentSyntax
{
    private static readonly Dictionary<string, (string LinePrefix, string? BlockStart, string? BlockEnd)> ExtensionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // C-style languages
            { ".cs", ("//", "/*", "*/") },
            { ".cpp", ("//", "/*", "*/") },
            { ".c", ("//", "/*", "*/") },
            { ".h", ("//", "/*", "*/") },
            { ".hpp", ("//", "/*", "*/") },
            { ".cc", ("//", "/*", "*/") },
            { ".cxx", ("//", "/*", "*/") },
            { ".java", ("//", "/*", "*/") },
            { ".go", ("//", "/*", "*/") },
            { ".rs", ("//", "/*", "*/") },
            { ".js", ("//", "/*", "*/") },
            { ".mjs", ("//", "/*", "*/") },
            { ".cjs", ("//", "/*", "*/") },
            { ".ts", ("//", "/*", "*/") },
            { ".tsx", ("//", "/*", "*/") },
            { ".jsx", ("//", "/*", "*/") },
            { ".jsonc", ("//", "/*", "*/") },
            { ".swift", ("//", "/*", "*/") },
            { ".kt", ("//", "/*", "*/") },
            { ".kts", ("//", "/*", "*/") },
            { ".dart", ("//", "/*", "*/") },
            { ".scala", ("//", "/*", "*/") },
            { ".groovy", ("//", "/*", "*/") },
            { ".glsl", ("//", "/*", "*/") },
            { ".hlsl", ("//", "/*", "*/") },
            { ".vert", ("//", "/*", "*/") },
            { ".frag", ("//", "/*", "*/") },
            { ".geom", ("//", "/*", "*/") },

            // Hash comment languages
            { ".py", ("#", "\"\"\"", "\"\"\"") },
            { ".sh", ("#", null, null) },
            { ".bash", ("#", null, null) },
            { ".zsh", ("#", null, null) },
            { ".yaml", ("#", null, null) },
            { ".yml", ("#", null, null) },
            { ".rb", ("#", "=begin", "=end") },
            { ".pl", ("#", "=pod", "=cut") },
            { ".r", ("#", null, null) },
            { ".toml", ("#", null, null) },
            { ".dockerfile", ("#", null, null) },
            { "dockerfile", ("#", null, null) },
            { ".ps1", ("#", "<#", "#>") },
            { ".psm1", ("#", "<#", "#>") },
            { ".psd1", ("#", "<#", "#>") },

            // Markup and XML-like
            { ".html", ("<!--", "<!--", "-->") },
            { ".htm", ("<!--", "<!--", "-->") },
            { ".xml", ("<!--", "<!--", "-->") },
            { ".axaml", ("<!--", "<!--", "-->") },
            { ".xaml", ("<!--", "<!--", "-->") },
            { ".svg", ("<!--", "<!--", "-->") },
            { ".vue", ("<!--", "<!--", "-->") },
            { ".svelte", ("<!--", "<!--", "-->") },

            // Stylesheets
            { ".css", ("/*", "/*", "*/") },
            { ".scss", ("//", "/*", "*/") },
            { ".less", ("//", "/*", "*/") },

            // Database & Scripts
            { ".sql", ("--", "/*", "*/") },
            { ".lua", ("--", "--[[", "]]") },

            // Configs & Batch
            { ".ini", (";", null, null) },
            { ".cfg", ("#", null, null) },
            { ".conf", ("#", null, null) },
            { ".properties", ("#", null, null) },
            { ".bat", ("REM", null, null) },
            { ".cmd", ("REM", null, null) }
        };

    /// <summary>
    /// Gets the comment syntax for a given file path or file name.
    /// </summary>
    public static (string LinePrefix, string? BlockStart, string? BlockEnd) GetCommentSyntax(string? filePath, string defaultPrefix = "//")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return (defaultPrefix, "/*", "*/");

        var fileName = Path.GetFileName(filePath);
        if (ExtensionMap.TryGetValue(fileName, out var exactMatch))
            return exactMatch;

        var ext = Path.GetExtension(filePath);
        if (!string.IsNullOrEmpty(ext) && ExtensionMap.TryGetValue(ext, out var extMatch))
            return extMatch;

        return (defaultPrefix, "/*", "*/");
    }

    /// <summary>
    /// Extracts comment syntax from a LanguageDefinition if available, falling back to file extension rules.
    /// </summary>
    public static (string LinePrefix, string? BlockStart, string? BlockEnd) Resolve(
        LanguageDefinition? languageDefinition,
        string? filePath,
        string defaultPrefix = "//")
    {
        if (languageDefinition != null)
        {
            var linePrefix = !string.IsNullOrEmpty(languageDefinition.LineCommentPrefix)
                ? languageDefinition.LineCommentPrefix
                : defaultPrefix;

            return (linePrefix, languageDefinition.BlockCommentStart, languageDefinition.BlockCommentEnd);
        }

        return GetCommentSyntax(filePath, defaultPrefix);
    }
}
