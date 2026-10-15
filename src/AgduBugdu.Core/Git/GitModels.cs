using System;
using System.Collections.Generic;
using System.IO;

namespace AgduBugdu.Core.Git;

public enum GitFileStatusType
{
    Modified,
    Added,
    Deleted,
    Untracked,
    Renamed,
    Copied,
    Conflict,
    Unknown
}

public record GitFileItem(
    string FilePath,
    string RelativePath,
    GitFileStatusType Status,
    bool IsStaged
)
{
    public string DisplayName => Path.GetFileName(RelativePath);
    public string DirectoryName => Path.GetDirectoryName(RelativePath) ?? string.Empty;

    public string StatusBadge => Status switch
    {
        GitFileStatusType.Modified => "M",
        GitFileStatusType.Added => "A",
        GitFileStatusType.Deleted => "D",
        GitFileStatusType.Untracked => "U",
        GitFileStatusType.Renamed => "R",
        GitFileStatusType.Copied => "C",
        GitFileStatusType.Conflict => "!",
        _ => "?"
    };
}

public record GitStatusResult(
    bool IsGitRepository,
    string? CurrentBranch,
    int AheadCount,
    int BehindCount,
    IReadOnlyList<GitFileItem> StagedFiles,
    IReadOnlyList<GitFileItem> UnstagedFiles,
    IReadOnlyList<GitFileItem> UntrackedFiles
)
{
    public int TotalChangesCount => StagedFiles.Count + UnstagedFiles.Count + UntrackedFiles.Count;

    public static GitStatusResult NotARepository() =>
        new(false, null, 0, 0, Array.Empty<GitFileItem>(), Array.Empty<GitFileItem>(), Array.Empty<GitFileItem>());
}

public record GitCommitResult(
    bool Success,
    string? CommitHash,
    string Message,
    string? Error = null
);

public record GitLogEntry(
    string Hash,
    string ShortHash,
    string Author,
    string Date,
    string Subject
);

public record GitBranch(
    string Name,
    bool IsCurrent,
    bool IsRemote,
    string? Upstream = null,
    string? CommitHash = null,
    string? CommitSubject = null
)
{
    public string DisplayColor => IsCurrent ? "#38BDF8" : (IsRemote ? "#94A3B8" : "#E2E8F0");
    public string DisplayFontWeight => IsCurrent ? "Bold" : "Normal";
}

public record GitTag(
    string Name,
    string? CommitHash = null,
    string? Annotation = null
);

public record GitStash(
    int Index,
    string Identifier,
    string Message,
    string Date
);

public record GitCommitFileChange(
    string Path,
    GitFileStatusType Status,
    int Additions,
    int Deletions
);

public record GitCommitDetail(
    string Hash,
    string ShortHash,
    string Author,
    string AuthorEmail,
    string Date,
    string Subject,
    string Body,
    IReadOnlyList<string> ParentHashes,
    IReadOnlyList<GitCommitFileChange> Files
);

public enum DiffLineKind
{
    Context,
    Addition,
    Deletion,
    HunkHeader
}

public class DiffLine
{
    public DiffLineKind Kind { get; set; }
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public string Text { get; set; } = string.Empty;

    public string BackgroundColor => Kind switch
    {
        DiffLineKind.Addition => "#153526",
        DiffLineKind.Deletion => "#3F1820",
        DiffLineKind.HunkHeader => "#1E293B",
        _ => "Transparent"
    };

    public string TextColor => Kind switch
    {
        DiffLineKind.Addition => "#4ADE80",
        DiffLineKind.Deletion => "#F87171",
        DiffLineKind.HunkHeader => "#38BDF8",
        _ => "#E2E8F0"
    };
}

public class DiffHunk
{
    public string Header { get; set; } = string.Empty;
    public int OldStart { get; set; }
    public int OldCount { get; set; }
    public int NewStart { get; set; }
    public int NewCount { get; set; }
    public List<DiffLine> Lines { get; } = new();
}

public class DiffFileModel
{
    public string OldPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public List<DiffHunk> Hunks { get; } = new();
}
