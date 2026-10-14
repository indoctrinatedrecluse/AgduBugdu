using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.Core;
using AgduBugdu.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public partial class UpdateModalViewModel : ViewModelBase
{
    private readonly IUpdateService? _updateService;
    private UpdateCheckResult? _checkResult;
    private CancellationTokenSource? _downloadCts;

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

    [ObservableProperty]
    private string _assetFileName = string.Empty;

    [ObservableProperty]
    private string _assetSizeFormatted = string.Empty;

    [ObservableProperty]
    private bool _hasDirectDownload;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _speedText = string.Empty;

    [ObservableProperty]
    private string _downloadedFilePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string VersionComparisonText => $"v{CurrentVersion} -> v{LatestVersion}";

    public string ModalTitle => IsDownloaded
        ? "Update Ready to Install"
        : (IsDownloading ? "Downloading Update..." : "Software Update Available");

    public bool CanStartDownload => !IsDownloading && !IsDownloaded;

    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartDownload));
        OnPropertyChanged(nameof(ModalTitle));
    }

    partial void OnIsDownloadedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartDownload));
        OnPropertyChanged(nameof(ModalTitle));
    }

    public UpdateModalViewModel()
    {
    }

    public UpdateModalViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public void ShowUpdate(UpdateCheckResult result)
    {
        _checkResult = result;
        LatestVersion = result.LatestVersion;
        ReleaseNotes = result.ReleaseNotes;
        ReleaseUrl = result.ReleaseUrl;
        AssetFileName = result.AssetFileName ?? string.Empty;
        AssetSizeFormatted = result.AssetSize > 0 ? $"{result.AssetSize / 1048576.0:F1} MB" : string.Empty;
        HasDirectDownload = !string.IsNullOrWhiteSpace(result.DownloadUrl);

        IsDownloading = false;
        IsDownloaded = false;
        HasError = false;
        ErrorMessage = string.Empty;
        DownloadProgress = 0;
        ProgressText = string.Empty;
        SpeedText = string.Empty;
        DownloadedFilePath = string.Empty;
        StatusMessage = string.Empty;

        OnPropertyChanged(nameof(VersionComparisonText));
        OnPropertyChanged(nameof(ModalTitle));
        IsVisible = true;
    }

    public void ShowUpdate(string latestVersion, string releaseNotes, string releaseUrl)
    {
        ShowUpdate(new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = CurrentVersion,
            LatestVersion = latestVersion,
            ReleaseNotes = releaseNotes,
            ReleaseUrl = releaseUrl
        });
    }

    [RelayCommand]
    public async Task DownloadAndInstallAsync()
    {
        if (IsDownloaded && !string.IsNullOrEmpty(DownloadedFilePath))
        {
            InstallAndRestart();
            return;
        }

        if (_updateService == null || _checkResult == null || string.IsNullOrWhiteSpace(_checkResult.DownloadUrl))
        {
            OpenReleaseUrl();
            return;
        }

        IsDownloading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        DownloadProgress = 0;
        ProgressText = "Starting download...";
        SpeedText = string.Empty;
        StatusMessage = "Downloading package...";
        OnPropertyChanged(nameof(ModalTitle));

        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();

        var progress = new Progress<UpdateDownloadProgress>(p =>
        {
            DownloadProgress = p.ProgressPercentage;
            ProgressText = p.FormattedProgress;
            SpeedText = p.FormattedSpeed;
        });

        try
        {
            var downloadedFile = await _updateService.DownloadUpdateAsync(_checkResult, progress, _downloadCts.Token);
            DownloadedFilePath = downloadedFile;

            if (!string.IsNullOrWhiteSpace(_checkResult.ChecksumsUrl))
            {
                ProgressText = "Verifying package integrity...";
                bool isValid = await _updateService.VerifyChecksumAsync(downloadedFile, _checkResult.ChecksumsUrl, _downloadCts.Token);
                if (!isValid)
                {
                    HasError = true;
                    ErrorMessage = "Checksum verification failed! Downloaded package may be corrupted or altered.";
                    IsDownloading = false;
                    OnPropertyChanged(nameof(ModalTitle));
                    return;
                }
            }

            IsDownloading = false;
            IsDownloaded = true;
            DownloadProgress = 100;
            ProgressText = "Download complete and verified!";
            SpeedText = string.Empty;
            StatusMessage = "Ready to install. Click 'Restart & Install' to proceed.";
            OnPropertyChanged(nameof(ModalTitle));
        }
        catch (OperationCanceledException)
        {
            IsDownloading = false;
            ProgressText = "Download cancelled.";
            SpeedText = string.Empty;
            OnPropertyChanged(nameof(ModalTitle));
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            HasError = true;
            ErrorMessage = $"Download failed: {ex.Message}";
            SpeedText = string.Empty;
            OnPropertyChanged(nameof(ModalTitle));
        }
    }

    [RelayCommand]
    public void InstallAndRestart()
    {
        if (string.IsNullOrWhiteSpace(DownloadedFilePath))
            return;

        _updateService?.LaunchInstallerAndExit(DownloadedFilePath);
    }

    [RelayCommand]
    public void CancelDownload()
    {
        _downloadCts?.Cancel();
        IsDownloading = false;
        ProgressText = "Download cancelled.";
        OnPropertyChanged(nameof(ModalTitle));
    }

    [RelayCommand]
    public void Dismiss()
    {
        if (IsDownloading)
        {
            _downloadCts?.Cancel();
            IsDownloading = false;
        }

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
                    ShowUpdate(result);
                });
            }
        }
        catch (Exception)
        {
            // Ignore background check errors
        }
    }
}
