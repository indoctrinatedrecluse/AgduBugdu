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
