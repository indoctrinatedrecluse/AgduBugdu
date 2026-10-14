using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Text;

public record WorkspaceSearchMatch(
    string FilePath,
    string RelativePath,
    int LineNumber,
    int ColumnNumber,
    int MatchLength,
    string LineText,
    string MatchedText
);

public record WorkspaceFileSearchResult(
    string FilePath,
    string RelativePath,
    IReadOnlyList<WorkspaceSearchMatch> Matches
);

public record WorkspaceSearchSummary(
    int TotalFilesSearched,
    int MatchingFilesCount,
    int TotalMatchesCount,
    TimeSpan Elapsed,
    IReadOnlyList<WorkspaceFileSearchResult> FileResults
);

public record WorkspaceSearchOptions(
    string Query,
    bool MatchCase = false,
    bool WholeWord = false,
    bool UseRegex = false,
    string? FilesToInclude = null,
    string? FilesToExclude = null,
    int MaxResults = 5000
);

public static class WorkspaceSearchEngine
{
    private static readonly HashSet<string> DefaultExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", "bin", "obj", "node_modules", ".vs", ".idea", ".vscode", "dist", "build", "out", "target", "vendor"
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".so", ".dylib",
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".bmp", ".webp", ".tiff",
        ".pdf", ".zip", ".tar", ".gz", ".7z", ".rar", ".iso",
        ".woff", ".woff2", ".ttf", ".eot", ".mp3", ".mp4", ".wav", ".avi", ".mov"
    };

    public static async Task<WorkspaceSearchSummary> SearchWorkspaceAsync(
        string rootPath,
        WorkspaceSearchOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath) || string.IsNullOrEmpty(options.Query))
        {
            return new WorkspaceSearchSummary(0, 0, 0, TimeSpan.Zero, Array.Empty<WorkspaceFileSearchResult>());
        }

        var customExcludes = ParsePatterns(options.FilesToExclude);
        var customIncludes = ParsePatterns(options.FilesToInclude);

        var fileResults = new List<WorkspaceFileSearchResult>();
        int totalFilesSearched = 0;
        int totalMatchesCount = 0;

        var singleFileOptions = new SearchOptions(
            options.Query,
            options.MatchCase,
            options.WholeWord,
            options.UseRegex
        );

        var filesToSearch = EnumerateCandidateFiles(rootPath, customExcludes, customIncludes, cancellationToken).ToList();

        foreach (var file in filesToSearch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalFilesSearched++;

            if (BinaryExtensions.Contains(Path.GetExtension(file)) || IsLikelyBinary(file))
            {
                continue;
            }

            try
            {
                string content = await File.ReadAllTextAsync(file, cancellationToken);
                var matches = SearchEngine.FindAll(content, singleFileOptions);

                if (matches.Count > 0)
                {
                    string relativePath = Path.GetRelativePath(rootPath, file);
                    var fileMatches = new List<WorkspaceSearchMatch>();

                    // Calculate line numbers and column numbers
                    var lineOffsets = ComputeLineOffsets(content);

                    foreach (var m in matches)
                    {
                        var (line, col) = GetLineAndColumn(lineOffsets, m.Offset);
                        string lineText = ExtractLineText(content, lineOffsets, line);

                        fileMatches.Add(new WorkspaceSearchMatch(
                            file,
                            relativePath,
                            line,
                            col,
                            m.Length,
                            lineText.Trim(),
                            m.Value
                        ));

                        totalMatchesCount++;
                        if (totalMatchesCount >= options.MaxResults)
                            break;
                    }

                    if (fileMatches.Count > 0)
                    {
                        fileResults.Add(new WorkspaceFileSearchResult(file, relativePath, fileMatches));
                    }

                    if (totalMatchesCount >= options.MaxResults)
                        break;
                }
            }
            catch
            {
                // Ignore file read/access exceptions gracefully
            }

            progress?.Report(totalFilesSearched);
        }

        var elapsed = DateTime.UtcNow - startTime;
        return new WorkspaceSearchSummary(
            totalFilesSearched,
            fileResults.Count,
            totalMatchesCount,
            elapsed,
            fileResults
        );
    }

    public static async Task<int> ReplaceInFileAsync(
        string filePath,
        WorkspaceSearchOptions options,
        string replacement,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath) || string.IsNullOrEmpty(options.Query))
            return 0;

        var singleFileOptions = new SearchOptions(
            options.Query,
            options.MatchCase,
            options.WholeWord,
            options.UseRegex
        );

        string content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var (newText, count) = SearchEngine.ReplaceAll(content, singleFileOptions, replacement);

        if (count > 0)
        {
            await File.WriteAllTextAsync(filePath, newText, cancellationToken);
        }

        return count;
    }

    public static async Task<(int TotalReplacements, int FilesModified)> ReplaceAllAcrossWorkspaceAsync(
        string rootPath,
        WorkspaceSearchOptions options,
        string replacement,
        CancellationToken cancellationToken = default)
    {
        var searchResult = await SearchWorkspaceAsync(rootPath, options, null, cancellationToken);
        int totalReplacements = 0;
        int filesModified = 0;

        foreach (var fileResult in searchResult.FileResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = await ReplaceInFileAsync(fileResult.FilePath, options, replacement, cancellationToken);
            if (count > 0)
            {
                totalReplacements += count;
                filesModified++;
            }
        }

        return (totalReplacements, filesModified);
    }

    private static IEnumerable<string> EnumerateCandidateFiles(
        string rootPath,
        List<string> customExcludes,
        List<string> customIncludes,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string dir = stack.Pop();

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (var subDir in subDirs)
            {
                string name = Path.GetFileName(subDir);
                if (DefaultExcludedDirectories.Contains(name))
                    continue;

                if (customExcludes.Any(pattern => MatchesWildcard(name, pattern)))
                    continue;

                stack.Push(subDir);
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                if (customExcludes.Any(pattern => MatchesWildcard(fileName, pattern)))
                    continue;

                if (customIncludes.Count > 0 && !customIncludes.Any(pattern => MatchesWildcard(fileName, pattern)))
                    continue;

                yield return file;
            }
        }
    }

    private static List<string> ParsePatterns(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    private static bool MatchesWildcard(string input, string pattern)
    {
        if (pattern == "*" || pattern == "*.*")
            return true;

        if (!pattern.Contains('*') && !pattern.Contains('?'))
        {
            return input.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsLikelyBinary(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            byte[] buffer = new byte[Math.Min(512, (int)stream.Length)];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static List<int> ComputeLineOffsets(string text)
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

    private static (int Line, int Column) GetLineAndColumn(List<int> lineOffsets, int offset)
    {
        int index = lineOffsets.BinarySearch(offset);
        if (index >= 0)
        {
            return (index + 1, 1);
        }

        int lineIndex = ~index - 1;
        if (lineIndex < 0) lineIndex = 0;
        int lineStart = lineOffsets[lineIndex];
        int column = offset - lineStart + 1;
        return (lineIndex + 1, column);
    }

    private static string ExtractLineText(string text, List<int> lineOffsets, int lineNumber)
    {
        int lineIndex = lineNumber - 1;
        if (lineIndex < 0 || lineIndex >= lineOffsets.Count)
            return string.Empty;

        int start = lineOffsets[lineIndex];
        int end = lineIndex + 1 < lineOffsets.Count ? lineOffsets[lineIndex + 1] - 1 : text.Length;

        if (end > start && text[end - 1] == '\r')
            end--;

        if (end <= start)
            return string.Empty;

        return text.Substring(start, Math.Max(0, end - start));
    }
}
