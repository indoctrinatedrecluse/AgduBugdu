using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;
using AgduBugdu.Core;

namespace AgduBugdu.App.ViewModels;

public partial class AboutModalViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    public string Version => AppVersionInfo.CurrentVersion;

    public void Show()
    {
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
    }
}

