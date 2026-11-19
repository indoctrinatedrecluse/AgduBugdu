using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public partial class ExtensionDisplayItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IExtension? ExtensionInstance { get; set; }

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _actionButtonText = "Disable";

    [ObservableProperty]
    private string _statusColor = "#34D399"; // Green

    [ObservableProperty]
    private string _statusBgColor = "#064E3B";
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

    [RelayCommand]
    public async Task ToggleExtensionAsync(ExtensionDisplayItem? item)
    {
        if (item == null) return;

        try
        {
            if (item.IsActive)
            {
                if (item.ExtensionInstance != null)
                {
                    await item.ExtensionInstance.DeactivateAsync();
                }
                item.IsActive = false;
                item.Status = "Disabled";
                item.ActionButtonText = "Enable";
                item.StatusColor = "#9CA3AF";
                item.StatusBgColor = "#27272A";
            }
            else
            {
                if (item.ExtensionInstance != null)
                {
                    await item.ExtensionInstance.ActivateAsync();
                }
                item.IsActive = true;
                item.Status = "Active";
                item.ActionButtonText = "Disable";
                item.StatusColor = "#34D399";
                item.StatusBgColor = "#064E3B";
            }
        }
        catch (Exception ex)
        {
            item.Status = $"Error: {ex.Message}";
            item.StatusColor = "#F87171";
            item.StatusBgColor = "#450A0A";
        }
    }
}
