using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public class ExtensionDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string Description { get; set; } = string.Empty;
}

public partial class ExtensionsModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible;

    public ObservableCollection<ExtensionDisplayItem> Extensions { get; } = new();

    public ExtensionsModalViewModel()
    {
    }

    public void Show()
    {
        IsVisible = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
    }
}
