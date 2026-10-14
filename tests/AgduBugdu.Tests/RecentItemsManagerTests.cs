using System;
using System.IO;
using AgduBugdu.Infrastructure.History;
using Xunit;

namespace AgduBugdu.Tests;

public class RecentItemsManagerTests : IDisposable
{
    private readonly string _tempFile;
    private readonly string _testFile1;
    private readonly string _testFile2;
    private readonly string _testFolder1;
    private readonly string _testFolder2;

    public RecentItemsManagerTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"recent_test_{Guid.NewGuid():N}.json");

        _testFile1 = Path.Combine(Path.GetTempPath(), $"f1_{Guid.NewGuid():N}.txt");
        _testFile2 = Path.Combine(Path.GetTempPath(), $"f2_{Guid.NewGuid():N}.txt");
        File.WriteAllText(_testFile1, "file1");
        File.WriteAllText(_testFile2, "file2");

        _testFolder1 = Path.Combine(Path.GetTempPath(), $"dir1_{Guid.NewGuid():N}");
        _testFolder2 = Path.Combine(Path.GetTempPath(), $"dir2_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testFolder1);
        Directory.CreateDirectory(_testFolder2);
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
        try { if (File.Exists(_testFile1)) File.Delete(_testFile1); } catch { }
        try { if (File.Exists(_testFile2)) File.Delete(_testFile2); } catch { }
        try { if (Directory.Exists(_testFolder1)) Directory.Delete(_testFolder1, true); } catch { }
        try { if (Directory.Exists(_testFolder2)) Directory.Delete(_testFolder2, true); } catch { }
    }

    [Fact]
    public void AddRecentFile_OrdersMostRecentFirst_DeduplicatesEntries()
    {
        var manager = new RecentItemsManager(_tempFile);

        manager.AddRecentFile(_testFile1);
        manager.AddRecentFile(_testFile2);
        manager.AddRecentFile(_testFile1); // re-add file1, should move to index 0

        var recents = manager.GetRecentFiles(10);
        Assert.Equal(2, recents.Count);
        Assert.Equal(Path.GetFullPath(_testFile1), recents[0]);
        Assert.Equal(Path.GetFullPath(_testFile2), recents[1]);
    }

    [Fact]
    public void AddRecentFolder_OrdersMostRecentFirst()
    {
        var manager = new RecentItemsManager(_tempFile);

        manager.AddRecentFolder(_testFolder1);
        manager.AddRecentFolder(_testFolder2);

        var recents = manager.GetRecentFolders(10);
        Assert.Equal(2, recents.Count);
        Assert.Equal(Path.GetFullPath(_testFolder2), recents[0]);
        Assert.Equal(Path.GetFullPath(_testFolder1), recents[1]);
    }

    [Fact]
    public void Persistence_LoadsExistingDataOnInitialization()
    {
        var manager1 = new RecentItemsManager(_tempFile);
        manager1.AddRecentFile(_testFile1);
        manager1.AddRecentFolder(_testFolder1);

        // Create new instance pointing to same file
        var manager2 = new RecentItemsManager(_tempFile);
        var files = manager2.GetRecentFiles(10);
        var folders = manager2.GetRecentFolders(10);

        Assert.Single(files);
        Assert.Equal(Path.GetFullPath(_testFile1), files[0]);
        Assert.Single(folders);
        Assert.Equal(Path.GetFullPath(_testFolder1), folders[0]);
    }

    [Fact]
    public void RemoveAndClear_RemovesEntriesCorrectly()
    {
        var manager = new RecentItemsManager(_tempFile);
        manager.AddRecentFile(_testFile1);
        manager.AddRecentFile(_testFile2);

        manager.RemoveRecentFile(Path.GetFullPath(_testFile1));
        var remaining = manager.GetRecentFiles(10);
        Assert.Single(remaining);
        Assert.Equal(Path.GetFullPath(_testFile2), remaining[0]);

        manager.ClearAll();
        Assert.Empty(manager.GetRecentFiles(10));
        Assert.Empty(manager.GetRecentFolders(10));
    }

    [Fact]
    public void Events_FiresWhenItemsAddedOrCleared()
    {
        var manager = new RecentItemsManager(_tempFile);
        int eventCount = 0;
        manager.RecentItemsChanged += (s, e) => eventCount++;

        manager.AddRecentFile(_testFile1);
        manager.AddRecentFolder(_testFolder1);
        manager.ClearAll();

        Assert.Equal(3, eventCount);
    }
}
