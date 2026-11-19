using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgduBugdu.App.Models;

public partial class FileSystemItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }

    public bool IsMarkdownFile => !IsDirectory && FullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    public bool IsCsvFile => !IsDirectory && (FullPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || FullPath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase));
    public bool IsPdfFile => !IsDirectory && FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<FileSystemItem> Children { get; } = new();

    private bool _hasLoadedChildren;

    public void LoadChildren()
    {
        if (_hasLoadedChildren || !IsDirectory)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(FullPath) || !Directory.Exists(FullPath))
            {
                Children.Clear();
                return;
            }

            var dirInfo = new DirectoryInfo(FullPath);
            var loadedItems = new System.Collections.Generic.List<FileSystemItem>();

            foreach (var dir in dirInfo.GetDirectories())
            {
                // Skip hidden/system directories like .git, obj, bin
                if (dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Name.Equals(".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                var item = new FileSystemItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };
                // Add dummy child so it shows expand arrow
                item.Children.Add(new FileSystemItem { Name = "Loading..." });
                loadedItems.Add(item);
            }

            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                loadedItems.Add(new FileSystemItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false
                });
            }

            Children.Clear();
            foreach (var item in loadedItems)
            {
                Children.Add(item);
            }
        }
        catch (Exception)
        {
            // Handle permission/access denied gracefully
            Children.Clear();
        }
        finally
        {
            _hasLoadedChildren = true;
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_hasLoadedChildren)
        {
            LoadChildren();
        }
    }
}
