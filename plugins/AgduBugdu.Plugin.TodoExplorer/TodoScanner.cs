using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AgduBugdu.Plugin.TodoExplorer;

public class TodoItem
{
    public string Tag { get; set; } = "TODO";
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public int LineNumber { get; set; } = 1;
    public int Column { get; set; } = 1;

    public string TagColor => Tag.ToUpperInvariant() switch
    {
        "FIXME" or "BUG" => "#EF4444", // Red
        "HACK" or "XXX" => "#F59E0B",  // Orange/Amber
        "NOTE" => "#10B981",          // Green
        _ => "#3B82F6"                 // Blue (TODO)
    };

    public string DisplayLocation => $"{RelativePath}:{LineNumber}";
    public string DisplayTitle => $"[{Tag}] {Message}";
}

public static class TodoScanner
{
    private static readonly Regex TodoPattern = new(
        @"(?i)(?://|/\*|#|<!--|;|--)\s*(TODO|FIXME|BUG|HACK|NOTE|XXX)\s*[:\-]?\s*(.*)",
        RegexOptions.Compiled
    );

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".js", ".py", ".c", ".cpp", ".h", ".rs", ".go", ".java",
        ".html", ".css", ".json", ".md", ".txt", ".axaml", ".xaml", ".xml", ".sh", ".ps1"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "dist", "publish", "artifacts"
    };

    public static List<TodoItem> ScanWorkspace(string rootDirectory)
    {
        var results = new List<TodoItem>();
        if (!Directory.Exists(rootDirectory)) return results;

        try
        {
            var dirInfo = new DirectoryInfo(rootDirectory);
            ScanDirectory(dirInfo, rootDirectory, results);
        }
        catch
        {
            // Ignore access errors
        }

        return results;
    }

    public static List<TodoItem> ScanFile(string filePath, string rootDirectory)
    {
        var list = new List<TodoItem>();
        if (!File.Exists(filePath)) return list;

        try
        {
            var ext = Path.GetExtension(filePath);
            if (!SupportedExtensions.Contains(ext)) return list;

            var lines = File.ReadAllLines(filePath);
            var relPath = Path.GetRelativePath(rootDirectory, filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = TodoPattern.Match(lines[i]);
                if (match.Success)
                {
                    var tag = match.Groups[1].Value.ToUpperInvariant();
                    var msg = match.Groups[2].Value.Trim().TrimEnd('*', '/', '-', '>');
                    if (string.IsNullOrWhiteSpace(msg)) msg = "(No description)";

                    list.Add(new TodoItem
                    {
                        Tag = tag,
                        Message = msg,
                        FilePath = filePath,
                        RelativePath = relPath,
                        LineNumber = i + 1,
                        Column = match.Index + 1
                    });
                }
            }
        }
        catch
        {
            // Ignore read errors
        }

        return list;
    }

    private static void ScanDirectory(DirectoryInfo dir, string rootPath, List<TodoItem> results)
    {
        if (dir.Attributes.HasFlag(FileAttributes.Hidden) || IgnoredDirectories.Contains(dir.Name))
        {
            return;
        }

        try
        {
            foreach (var file in dir.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                var ext = file.Extension;
                if (!SupportedExtensions.Contains(ext)) continue;

                results.AddRange(ScanFile(file.FullName, rootPath));
            }

            foreach (var subDir in dir.GetDirectories())
            {
                ScanDirectory(subDir, rootPath, results);
            }
        }
        catch
        {
            // Skip unreadable directories
        }
    }
}
