using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AgduBugdu.Infrastructure.History;

public class RecentItemsData
{
    public List<string> RecentFiles { get; set; } = new();
    public List<string> RecentFolders { get; set; } = new();
}

public interface IRecentItemsManager
{
    event EventHandler? RecentItemsChanged;
    void AddRecentFile(string filePath);
    void AddRecentFolder(string folderPath);
    void RemoveRecentFile(string filePath);
    void RemoveRecentFolder(string folderPath);
    void ClearAll();
    IReadOnlyList<string> GetRecentFiles(int limit = 15);
    IReadOnlyList<string> GetRecentFolders(int limit = 10);
}

public class RecentItemsManager : IRecentItemsManager
{
    private readonly string _storageFilePath;
    private readonly object _lock = new();
    private RecentItemsData _data = new();

    public event EventHandler? RecentItemsChanged;

    public RecentItemsManager(string? customStoragePath = null)
    {
        if (!string.IsNullOrEmpty(customStoragePath))
        {
            _storageFilePath = customStoragePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "AgduBugdu");
            _storageFilePath = Path.Combine(folder, "recent_items.json");
        }

        Load();
    }

    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        string normalized = Path.GetFullPath(filePath);
        lock (_lock)
        {
            _data.RecentFiles.RemoveAll(f => string.Equals(f, normalized, StringComparison.OrdinalIgnoreCase));
            _data.RecentFiles.Insert(0, normalized);

            if (_data.RecentFiles.Count > 50)
            {
                _data.RecentFiles = _data.RecentFiles.Take(50).ToList();
            }

            Save();
        }
        RecentItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddRecentFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        string normalized = Path.GetFullPath(folderPath);
        lock (_lock)
        {
            _data.RecentFolders.RemoveAll(f => string.Equals(f, normalized, StringComparison.OrdinalIgnoreCase));
            _data.RecentFolders.Insert(0, normalized);

            if (_data.RecentFolders.Count > 30)
            {
                _data.RecentFolders = _data.RecentFolders.Take(30).ToList();
            }

            Save();
        }
        RecentItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveRecentFile(string filePath)
    {
        lock (_lock)
        {
            _data.RecentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
            Save();
        }
        RecentItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveRecentFolder(string folderPath)
    {
        lock (_lock)
        {
            _data.RecentFolders.RemoveAll(f => string.Equals(f, folderPath, StringComparison.OrdinalIgnoreCase));
            Save();
        }
        RecentItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _data.RecentFiles.Clear();
            _data.RecentFolders.Clear();
            Save();
        }
        RecentItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<string> GetRecentFiles(int limit = 15)
    {
        lock (_lock)
        {
            return _data.RecentFiles
                .Where(File.Exists)
                .Take(limit)
                .ToList();
        }
    }

    public IReadOnlyList<string> GetRecentFolders(int limit = 10)
    {
        lock (_lock)
        {
            return _data.RecentFolders
                .Where(Directory.Exists)
                .Take(limit)
                .ToList();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                string json = File.ReadAllText(_storageFilePath);
                var loaded = JsonSerializer.Deserialize<RecentItemsData>(json);
                if (loaded != null)
                {
                    _data = loaded;
                }
            }
        }
        catch
        {
            _data = new RecentItemsData();
        }
    }

    private void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storageFilePath, json);
        }
        catch
        {
            // Ignore write errors to keep editor operations non-blocking
        }
    }
}
