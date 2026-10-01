using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AgduBugdu.Core;
using AgduBugdu.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public partial class UpdateModalViewModel : ViewModelBase
{
    private readonly IUpdateService? _updateService;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _currentVersion = AppVersionInfo.CurrentVersion;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private string _releaseUrl = string.Empty;

    public string VersionComparisonText => $"v{CurrentVersion} -> v{LatestVersion}";

    public UpdateModalViewModel()
    {
    }

    public UpdateModalViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public void ShowUpdate(string latestVersion, string releaseNotes, string releaseUrl)
    {
        LatestVersion = latestVersion;
        ReleaseNotes = releaseNotes;
        ReleaseUrl = releaseUrl;
        OnPropertyChanged(nameof(VersionComparisonText));
        IsVisible = true;
    }

    [RelayCommand]
    public void Dismiss()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public void OpenReleaseUrl()
    {
        try
        {
            var url = string.IsNullOrEmpty(ReleaseUrl)
                ? $"https://github.com/{AppVersionInfo.RepositoryOwner}/{AppVersionInfo.RepositoryName}/releases"
                : ReleaseUrl;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Ignore browser opening errors
        }

        IsVisible = false;
    }

    public async Task CheckForUpdatesSilentlyAsync()
    {
        if (_updateService == null)
            return;

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            if (result.IsUpdateAvailable)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ShowUpdate(result.LatestVersion, result.ReleaseNotes, result.ReleaseUrl);
                });
            }
        }
        catch (Exception)
        {
            // Ignore background check errors
        }
    }
}
