using System;
using System.Collections.ObjectModel;
using System.IO;
using AgduBugdu.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class ExplorerToolViewModel : Tool
{
    [ObservableProperty]
    private string? _currentWorkspacePath;

    [ObservableProperty]
    private string _workspaceName = "NO WORKSPACE";

    [ObservableProperty]
    private bool _hasWorkspace;

    public ObservableCollection<FileSystemItem> RootItems { get; } = new();

    [ObservableProperty]
    private FileSystemItem? _selectedItem;

    public event EventHandler<string>? FileSelected;
    public event EventHandler? OpenFolderRequested;

    public ExplorerToolViewModel()
    {
        Id = "Explorer";
        Title = "Explorer";
    }

    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        CurrentWorkspacePath = folderPath;
        WorkspaceName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToUpperInvariant();
        HasWorkspace = true;

        RootItems.Clear();

        try
        {
            var rootDir = new DirectoryInfo(folderPath);

            foreach (var dir in rootDir.GetDirectories())
            {
                if (dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Name == ".git")
                    continue;

                var item = new FileSystemItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };
                item.Children.Add(new FileSystemItem { Name = "Loading..." });
                RootItems.Add(item);
            }

            foreach (var file in rootDir.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                RootItems.Add(new FileSystemItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false
                });
            }
        }
        catch (Exception)
        {
            // Ignore access errors
        }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ItemDoubleClicked(FileSystemItem? item)
    {
        if (item == null)
            return;

        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            if (item.IsExpanded)
            {
                item.LoadChildren();
            }
        }
        else
        {
            FileSelected?.Invoke(this, item.FullPath);
        }
    }
}

public class OutputToolViewModel : Tool
{
    public OutputToolViewModel()
    {
        Id = "Output";
        Title = "Output";
    }
}
