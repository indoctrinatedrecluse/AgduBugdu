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
}
