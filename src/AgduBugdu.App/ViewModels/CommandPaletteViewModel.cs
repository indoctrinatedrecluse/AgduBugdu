using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public partial class CommandPaletteItem : ObservableObject
{
    public string Id { get; }
    public string Title { get; }
    public string? Shortcut { get; }
    public Action ExecuteAction { get; }

    public CommandPaletteItem(string id, string title, Action executeAction, string? shortcut = null)
    {
        Id = id;
        Title = title;
        ExecuteAction = executeAction;
        Shortcut = shortcut;
    }
}

public partial class CommandPaletteViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<CommandPaletteItem> AllCommands { get; } = new();
    public ObservableCollection<CommandPaletteItem> FilteredCommands { get; } = new();

    [ObservableProperty]
    private CommandPaletteItem? _selectedItem;

    public CommandPaletteViewModel()
    {
    }

    public void RegisterCommand(string id, string title, Action action, string? shortcut = null)
    {
        var item = new CommandPaletteItem(id, title, action, shortcut);
        AllCommands.Add(item);
        Filter();
    }

    partial void OnSearchTextChanged(string value)
    {
        Filter();
    }

    private void Filter()
    {
        FilteredCommands.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        foreach (var cmd in AllCommands)
        {
            if (string.IsNullOrEmpty(query) || cmd.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredCommands.Add(cmd);
            }
        }

        if (FilteredCommands.Count > 0)
        {
            SelectedItem = FilteredCommands[0];
        }
        else
        {
            SelectedItem = null;
        }
    }

    [RelayCommand]
    public void ExecuteSelected(CommandPaletteItem? target = null)
    {
        target ??= SelectedItem;
        if (target != null)
        {
            var action = target.ExecuteAction;
            IsOpen = false;
            SearchText = string.Empty;
            action?.Invoke();
        }
    }

    [RelayCommand]
    public void Open()
    {
        SearchText = string.Empty;
        Filter();
        IsOpen = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        SearchText = string.Empty;
    }
}

