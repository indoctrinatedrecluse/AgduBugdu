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
            Children.Clear();
            var dirInfo = new DirectoryInfo(FullPath);

            foreach (var dir in dirInfo.GetDirectories())
            {
                // Skip hidden/system directories like .git, obj, bin
                if (dir.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                var item = new FileSystemItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };
                // Add dummy child so it shows expand arrow
                item.Children.Add(new FileSystemItem { Name = "Loading..." });
                Children.Add(item);
            }

            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                Children.Add(new FileSystemItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false
                });
            }

            _hasLoadedChildren = true;
        }
        catch (Exception)
        {
            // Handle permission/access denied gracefully
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
