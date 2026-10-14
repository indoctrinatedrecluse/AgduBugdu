using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgduBugdu.Core;

namespace AgduBugdu.App.ViewModels;

public partial class AboutModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible;

    public string Version => AppVersionInfo.CurrentVersion;

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
