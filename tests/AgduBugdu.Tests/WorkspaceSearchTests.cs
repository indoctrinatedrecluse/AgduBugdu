using System;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.Core.Text;
using Xunit;

namespace AgduBugdu.Tests;

public class WorkspaceSearchTests : IDisposable
{
    private readonly string _testDir;

    public WorkspaceSearchTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "AgduBugdu_SearchTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        // Create sample folder structure
        Directory.CreateDirectory(Path.Combine(_testDir, "src"));
        Directory.CreateDirectory(Path.Combine(_testDir, "bin")); // should be ignored by default
        Directory.CreateDirectory(Path.Combine(_testDir, "docs"));

        File.WriteAllText(Path.Combine(_testDir, "src", "Program.cs"),
            "namespace SampleApp;\nclass Program {\n    static void Main(string[] args) {\n        Console.WriteLine(\"Hello World!\");\n    }\n}\n");

        File.WriteAllText(Path.Combine(_testDir, "src", "Helper.cs"),
            "namespace SampleApp;\npublic class Helper {\n    public static string GetGreeting() => \"Hello AgduBugdu\";\n}\n");

        File.WriteAllText(Path.Combine(_testDir, "docs", "README.md"),
            "# SampleApp\nThis is a sample project.\nHello from docs!\n");

        File.WriteAllText(Path.Combine(_testDir, "bin", "Output.txt"),
            "Hello in bin folder, should be ignored!\n");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public async Task SearchWorkspaceAsync_FindsMatchesInCandidateFiles_IgnoresBinDirectory()
    {
        var options = new WorkspaceSearchOptions("Hello");
        var summary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, options);

        Assert.Equal(3, summary.MatchingFilesCount);
        Assert.Equal(3, summary.TotalMatchesCount);
        Assert.DoesNotContain(summary.FileResults, r => r.RelativePath.Contains("bin"));
    }

    [Fact]
    public async Task SearchWorkspaceAsync_MatchCase_DistinguishesCasing()
    {
        var lowerOptions = new WorkspaceSearchOptions("hello", MatchCase: true);
        var lowerSummary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, lowerOptions);
        Assert.Equal(0, lowerSummary.TotalMatchesCount);

        var upperOptions = new WorkspaceSearchOptions("Hello", MatchCase: true);
        var upperSummary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, upperOptions);
        Assert.Equal(3, upperSummary.TotalMatchesCount);
    }

    [Fact]
    public async Task SearchWorkspaceAsync_WholeWord_MatchesExactWordsOnly()
    {
        var options = new WorkspaceSearchOptions("Hel", WholeWord: true);
        var summary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, options);
        Assert.Equal(0, summary.TotalMatchesCount);

        var wordOptions = new WorkspaceSearchOptions("Hello", WholeWord: true);
        var wordSummary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, wordOptions);
        Assert.Equal(3, wordSummary.TotalMatchesCount);
    }

    [Fact]
    public async Task SearchWorkspaceAsync_WithFilesToInclude_FiltersToTargetExtensions()
    {
        var options = new WorkspaceSearchOptions("Hello", FilesToInclude: "*.cs");
        var summary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, options);

        Assert.Equal(2, summary.MatchingFilesCount);
        Assert.All(summary.FileResults, r => Assert.EndsWith(".cs", r.FilePath));
    }

    [Fact]
    public async Task SearchWorkspaceAsync_WithFilesToExclude_SkipsSpecifiedFoldersOrFiles()
    {
        var options = new WorkspaceSearchOptions("Hello", FilesToExclude: "docs, *.md");
        var summary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, options);

        Assert.Equal(2, summary.MatchingFilesCount);
        Assert.DoesNotContain(summary.FileResults, r => r.RelativePath.Contains("docs"));
    }

    [Fact]
    public async Task SearchWorkspaceAsync_UseRegex_MatchesPatternCorrectly()
    {
        var options = new WorkspaceSearchOptions(@"H\w+o", UseRegex: true);
        var summary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_testDir, options);

        Assert.Equal(3, summary.TotalMatchesCount);
    }

    [Fact]
    public async Task ReplaceAllAcrossWorkspaceAsync_ReplacesTextAndReturnsCount()
    {
        var options = new WorkspaceSearchOptions("Hello");
        var (totalReplacements, filesModified) = await WorkspaceSearchEngine.ReplaceAllAcrossWorkspaceAsync(
            _testDir, options, "Greetings");

        Assert.Equal(3, totalReplacements);
        Assert.Equal(3, filesModified);

        // Verify content was actually updated on disk
        string programContent = await File.ReadAllTextAsync(Path.Combine(_testDir, "src", "Program.cs"));
        Assert.Contains("Greetings World!", programContent);
        Assert.DoesNotContain("Hello World!", programContent);
    }
}
