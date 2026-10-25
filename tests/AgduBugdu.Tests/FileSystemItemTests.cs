using System;
using System.IO;
using System.Linq;
using AgduBugdu.App.Models;
using AgduBugdu.App.ViewModels.Tools;
using Xunit;

namespace AgduBugdu.Tests;

public class FileSystemItemTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemItemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failure in temp
        }
    }

    [Fact]
    public void SettingIsExpanded_LoadsChildrenAndRemovesDummyLoadingItem()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir, "subfolder");
        Directory.CreateDirectory(subDir);
        var subFile = Path.Combine(_tempDir, "hello.txt");
        File.WriteAllText(subFile, "world");

        var item = new FileSystemItem
        {
            Name = Path.GetFileName(_tempDir),
            FullPath = _tempDir,
            IsDirectory = true
        };
        item.Children.Add(new FileSystemItem { Name = "Loading..." });

        Assert.Single(item.Children);
        Assert.Equal("Loading...", item.Children[0].Name);
        Assert.False(item.IsExpanded);

        // Act - expanding should automatically trigger LoadChildren()
        item.IsExpanded = true;

        // Assert
        Assert.True(item.IsExpanded);
        Assert.DoesNotContain(item.Children, c => c.Name == "Loading...");
        Assert.Equal(2, item.Children.Count);
        Assert.Contains(item.Children, c => c.Name == "subfolder" && c.IsDirectory);
        Assert.Contains(item.Children, c => c.Name == "hello.txt" && !c.IsDirectory);
    }

    [Fact]
    public void ExpandingEmptyDirectory_ClearsLoadingPlaceholder()
    {
        // Arrange
        var emptySubDir = Path.Combine(_tempDir, "emptyDir");
        Directory.CreateDirectory(emptySubDir);

        var item = new FileSystemItem
        {
            Name = "emptyDir",
            FullPath = emptySubDir,
            IsDirectory = true
        };
        item.Children.Add(new FileSystemItem { Name = "Loading..." });

        // Act
        item.IsExpanded = true;

        // Assert
        Assert.Empty(item.Children);
    }

    [Fact]
    public void LoadChildren_IgnoresGitDirectory()
    {
        // Arrange
        var gitDir = Path.Combine(_tempDir, ".git");
        Directory.CreateDirectory(gitDir);
        var realDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(realDir);

        var item = new FileSystemItem
        {
            Name = Path.GetFileName(_tempDir),
            FullPath = _tempDir,
            IsDirectory = true
        };
        item.Children.Add(new FileSystemItem { Name = "Loading..." });

        // Act
        item.IsExpanded = true;

        // Assert
        Assert.Single(item.Children);
        Assert.Equal("src", item.Children[0].Name);
    }

    [Fact]
    public void ExplorerToolViewModel_DoubleClickedExpandsAndLoadsChildren()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir, "myFolder");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file.cs"), "// code");

        var vm = new ExplorerToolViewModel();
        vm.LoadFolder(_tempDir);

        var folderItem = vm.RootItems.FirstOrDefault(i => i.Name == "myFolder");
        Assert.NotNull(folderItem);
        Assert.False(folderItem.IsExpanded);
        Assert.Single(folderItem.Children);
        Assert.Equal("Loading...", folderItem.Children[0].Name);

        // Act
        vm.ItemDoubleClicked(folderItem);

        // Assert
        Assert.True(folderItem.IsExpanded);
        Assert.DoesNotContain(folderItem.Children, c => c.Name == "Loading...");
        Assert.Single(folderItem.Children);
        Assert.Equal("file.cs", folderItem.Children[0].Name);
    }
}
