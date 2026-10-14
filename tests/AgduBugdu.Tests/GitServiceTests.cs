using System.IO;
using System.Threading.Tasks;
using AgduBugdu.Core.Git;
using Xunit;

namespace AgduBugdu.Tests;

public class GitServiceTests
{
    [Fact]
    public void ParseGitStatus_ParsesBranchAndAheadBehindCounts()
    {
        string gitOutput = "## feature-branch...origin/feature-branch [ahead 3, behind 1]\n" +
                           " M src/App.cs\n" +
                           "M  src/Core.cs\n" +
                           "?? newfile.txt\n";

        var status = GitService.ParseGitStatus(@"C:\TestRepo", gitOutput);

        Assert.True(status.IsGitRepository);
        Assert.Equal("feature-branch", status.CurrentBranch);
        Assert.Equal(3, status.AheadCount);
        Assert.Equal(1, status.BehindCount);
        Assert.Single(status.StagedFiles);
        Assert.Single(status.UnstagedFiles);
        Assert.Single(status.UntrackedFiles);
        Assert.Equal(3, status.TotalChangesCount);
    }

    [Fact]
    public void ParseGitStatus_CorrectlyClassifiesStagedAndUnstagedStatus()
    {
        string gitOutput = "## main\n" +
                           "A  src/Added.cs\n" +
                           "M  src/StagedModified.cs\n" +
                           " D src/Deleted.cs\n" +
                           "?? docs/notes.txt\n";

        var status = GitService.ParseGitStatus(@"C:\TestRepo", gitOutput);

        Assert.Equal(2, status.StagedFiles.Count);
        Assert.Equal("Added.cs", status.StagedFiles[0].DisplayName);
        Assert.Equal(GitFileStatusType.Added, status.StagedFiles[0].Status);
        Assert.Equal("A", status.StagedFiles[0].StatusBadge);

        Assert.Single(status.UnstagedFiles);
        Assert.Equal("Deleted.cs", status.UnstagedFiles[0].DisplayName);
        Assert.Equal(GitFileStatusType.Deleted, status.UnstagedFiles[0].Status);
        Assert.Equal("D", status.UnstagedFiles[0].StatusBadge);

        Assert.Single(status.UntrackedFiles);
        Assert.Equal("U", status.UntrackedFiles[0].StatusBadge);
    }

    [Fact]
    public void ParseGitStatus_HandlesRenamedFiles()
    {
        string gitOutput = "## main\n" +
                           "R  old_name.cs -> new_name.cs\n";

        var status = GitService.ParseGitStatus(@"C:\TestRepo", gitOutput);

        Assert.Single(status.StagedFiles);
        Assert.Equal("new_name.cs", status.StagedFiles[0].DisplayName);
        Assert.Equal(GitFileStatusType.Renamed, status.StagedFiles[0].Status);
        Assert.Equal("R", status.StagedFiles[0].StatusBadge);
    }

    [Fact]
    public async Task CommitAsync_EmptyMessage_ReturnsFailure()
    {
        var service = new GitService();
        var result = await service.CommitAsync(@"C:\TestRepo", "   ");

        Assert.False(result.Success);
        Assert.Equal("Commit message cannot be empty.", result.Error);
    }

    [Fact]
    public void GitFileItem_StatusBadges_MapExpectedCharacters()
    {
        var modified = new GitFileItem("C:/test.cs", "test.cs", GitFileStatusType.Modified, false);
        var added = new GitFileItem("C:/test.cs", "test.cs", GitFileStatusType.Added, true);
        var untracked = new GitFileItem("C:/test.cs", "test.cs", GitFileStatusType.Untracked, false);
        var conflict = new GitFileItem("C:/test.cs", "test.cs", GitFileStatusType.Conflict, false);

        Assert.Equal("M", modified.StatusBadge);
        Assert.Equal("A", added.StatusBadge);
        Assert.Equal("U", untracked.StatusBadge);
        Assert.Equal("!", conflict.StatusBadge);
    }

    [Fact]
    public void ParseDiff_ParsesHunksAdditionsDeletionsAndLineNumbers()
    {
        string rawDiff = @"
diff --git a/src/App.cs b/src/App.cs
index 1234567..89abcdef 100644
--- a/src/App.cs
+++ b/src/App.cs
@@ -10,4 +10,5 @@
 context line 1
-removed line
+added line 1
+added line 2
 context line 2
";
        var service = new GitService();
        var diff = service.ParseDiff(rawDiff);

        Assert.Equal("src/App.cs", diff.OldPath);
        Assert.Equal("src/App.cs", diff.NewPath);
        Assert.Equal(2, diff.Additions);
        Assert.Equal(1, diff.Deletions);
        Assert.Single(diff.Hunks);

        var hunk = diff.Hunks[0];
        Assert.Equal(10, hunk.OldStart);
        Assert.Equal(10, hunk.NewStart);
        Assert.Equal(5, hunk.Lines.Count);

        // Line 0: context
        Assert.Equal(DiffLineKind.Context, hunk.Lines[0].Kind);
        Assert.Equal(10, hunk.Lines[0].OldLineNumber);
        Assert.Equal(10, hunk.Lines[0].NewLineNumber);

        // Line 1: deletion
        Assert.Equal(DiffLineKind.Deletion, hunk.Lines[1].Kind);
        Assert.Equal(11, hunk.Lines[1].OldLineNumber);
        Assert.Null(hunk.Lines[1].NewLineNumber);

        // Line 2: addition
        Assert.Equal(DiffLineKind.Addition, hunk.Lines[2].Kind);
        Assert.Null(hunk.Lines[2].OldLineNumber);
        Assert.Equal(11, hunk.Lines[2].NewLineNumber);

        // Line 3: addition
        Assert.Equal(DiffLineKind.Addition, hunk.Lines[3].Kind);
        Assert.Null(hunk.Lines[3].OldLineNumber);
        Assert.Equal(12, hunk.Lines[3].NewLineNumber);

        // Line 4: context
        Assert.Equal(DiffLineKind.Context, hunk.Lines[4].Kind);
        Assert.Equal(12, hunk.Lines[4].OldLineNumber);
        Assert.Equal(13, hunk.Lines[4].NewLineNumber);
    }

    [Fact]
    public void GitSuite_Models_InstantiateProperly()
    {
        var branch = new GitBranch("feature/auth", true, false, "origin/feature/auth", "abcdef1", "Implement auth");
        Assert.Equal("feature/auth", branch.Name);
        Assert.True(branch.IsCurrent);
        Assert.False(branch.IsRemote);

        var tag = new GitTag("v1.3.2", "abcdef1", "Release v1.3.2");
        Assert.Equal("v1.3.2", tag.Name);

        var stash = new GitStash(0, "stash@{0}", "WIP: editor changes", "2026-10-14");
        Assert.Equal(0, stash.Index);

        var fileChange = new GitCommitFileChange("src/Main.cs", GitFileStatusType.Modified, 15, 3);
        Assert.Equal(15, fileChange.Additions);
        Assert.Equal(3, fileChange.Deletions);

        var detail = new GitCommitDetail(
            "1234567890abcdef", "1234567", "John Doe", "john@example.com", "2026-10-14",
            "Commit Subject", "Commit Body", new[] { "parent1" }, new[] { fileChange }
        );
        Assert.Equal("1234567", detail.ShortHash);
        Assert.Single(detail.Files);
    }
}
