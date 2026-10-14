using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.App.ViewModels;
using AgduBugdu.Core.Updates;
using AgduBugdu.Infrastructure.Updates;
using Xunit;

namespace AgduBugdu.Tests;

public class AutoUpdaterTests
{
    [Theory]
    [InlineData("1.2.0", "1.3.0", true)]
    [InlineData("1.2.0", "1.2.1", true)]
    [InlineData("1.2.0", "2.0.0", true)]
    [InlineData("v1.2.0", "v1.3.0", true)]
    [InlineData("V1.2.0", "1.3.0", true)]
    [InlineData("1.3.0", "1.2.0", false)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("v1.3.0", "v1.3.0", false)]
    [InlineData("1.3.0-preview.1", "1.3.0", false)]
    [InlineData("1.2.0", "1.3.0+build.123", true)]
    [InlineData("", "1.3.0", false)]
    [InlineData("1.2.0", "", false)]
    public void VersionComparison_EvaluatesCorrectly(string current, string latest, bool expectedResult)
    {
        bool result = GitHubUpdateService.IsNewerVersion(current, latest);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void SelectTargetAsset_Windows_PrefersSetupExe()
    {
        var assets = new List<GitHubReleaseAsset>
        {
            new() { Name = "AgduBugdu-v1.3.0-win-x64-portable.zip", BrowserDownloadUrl = "http://example.com/win.zip" },
            new() { Name = "AgduBugdu-v1.3.0-win-x64-setup.exe", BrowserDownloadUrl = "http://example.com/win-setup.exe" },
            new() { Name = "AgduBugdu-v1.3.0-linux-x64.tar.gz", BrowserDownloadUrl = "http://example.com/linux.tar.gz" }
        };

        var selected = GitHubUpdateService.SelectTargetAsset(assets, OSPlatform.Windows, Architecture.X64);

        Assert.NotNull(selected);
        Assert.Equal("AgduBugdu-v1.3.0-win-x64-setup.exe", selected.Name);
        Assert.Equal("http://example.com/win-setup.exe", selected.BrowserDownloadUrl);
    }

    [Fact]
    public void SelectTargetAsset_Windows_FallsBackToPortableZipIfNoExe()
    {
        var assets = new List<GitHubReleaseAsset>
        {
            new() { Name = "AgduBugdu-v1.3.0-win-x64-portable.zip", BrowserDownloadUrl = "http://example.com/win.zip" },
            new() { Name = "AgduBugdu-v1.3.0-linux-x64.tar.gz", BrowserDownloadUrl = "http://example.com/linux.tar.gz" }
        };

        var selected = GitHubUpdateService.SelectTargetAsset(assets, OSPlatform.Windows, Architecture.X64);

        Assert.NotNull(selected);
        Assert.Equal("AgduBugdu-v1.3.0-win-x64-portable.zip", selected.Name);
    }

    [Fact]
    public void SelectTargetAsset_Linux_SelectsTarGz()
    {
        var assets = new List<GitHubReleaseAsset>
        {
            new() { Name = "AgduBugdu-v1.3.0-win-x64-setup.exe", BrowserDownloadUrl = "http://example.com/win-setup.exe" },
            new() { Name = "AgduBugdu-v1.3.0-linux-x64.tar.gz", BrowserDownloadUrl = "http://example.com/linux.tar.gz" },
            new() { Name = "AgduBugdu-v1.3.0-osx-x64.tar.gz", BrowserDownloadUrl = "http://example.com/osx.tar.gz" }
        };

        var selected = GitHubUpdateService.SelectTargetAsset(assets, OSPlatform.Linux, Architecture.X64);

        Assert.NotNull(selected);
        Assert.Equal("AgduBugdu-v1.3.0-linux-x64.tar.gz", selected.Name);
    }

    [Fact]
    public void SelectTargetAsset_OSX_Arm64_SelectsArmPackage()
    {
        var assets = new List<GitHubReleaseAsset>
        {
            new() { Name = "AgduBugdu-v1.3.0-osx-x64.tar.gz", BrowserDownloadUrl = "http://example.com/osx-x64.tar.gz" },
            new() { Name = "AgduBugdu-v1.3.0-osx-arm64.tar.gz", BrowserDownloadUrl = "http://example.com/osx-arm64.tar.gz" }
        };

        var selected = GitHubUpdateService.SelectTargetAsset(assets, OSPlatform.OSX, Architecture.Arm64);

        Assert.NotNull(selected);
        Assert.Equal("AgduBugdu-v1.3.0-osx-arm64.tar.gz", selected.Name);
    }

    [Fact]
    public void SelectTargetAsset_EmptyList_ReturnsNull()
    {
        var assets = new List<GitHubReleaseAsset>();
        var selected = GitHubUpdateService.SelectTargetAsset(assets, OSPlatform.Windows, Architecture.X64);
        Assert.Null(selected);
    }

    [Fact]
    public void UpdateDownloadProgress_FormatsCorrectly()
    {
        var progress = new UpdateDownloadProgress(
            BytesDownloaded: 20 * 1024 * 1024,
            TotalBytes: 40 * 1024 * 1024,
            ProgressPercentage: 50.0,
            BytesPerSecond: 5 * 1024 * 1024
        );

        Assert.Equal("20.0 MB / 40.0 MB (50%)", progress.FormattedProgress);
        Assert.Equal("5.0 MB/s", progress.FormattedSpeed);
    }

    [Fact]
    public void UpdateModalViewModel_ShowUpdate_InitializesState()
    {
        var vm = new UpdateModalViewModel();

        var result = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "1.2.0",
            LatestVersion = "1.3.0",
            ReleaseNotes = "Exciting new features",
            ReleaseUrl = "https://github.com/org/repo/releases/tag/v1.3.0",
            DownloadUrl = "https://github.com/org/repo/releases/download/v1.3.0/setup.exe",
            AssetFileName = "AgduBugdu-v1.3.0-win-x64-setup.exe",
            AssetSize = 45 * 1024 * 1024
        };

        vm.ShowUpdate(result);

        Assert.True(vm.IsVisible);
        Assert.Equal("1.3.0", vm.LatestVersion);
        Assert.Equal("Exciting new features", vm.ReleaseNotes);
        Assert.Equal("AgduBugdu-v1.3.0-win-x64-setup.exe", vm.AssetFileName);
        Assert.Equal("45.0 MB", vm.AssetSizeFormatted);
        Assert.True(vm.HasDirectDownload);
        Assert.True(vm.CanStartDownload);
        Assert.False(vm.IsDownloading);
        Assert.False(vm.IsDownloaded);
        Assert.Equal("Software Update Available", vm.ModalTitle);
    }

    [Fact]
    public void UpdateModalViewModel_Dismiss_HidesModal()
    {
        var vm = new UpdateModalViewModel();
        vm.IsVisible = true;

        vm.Dismiss();

        Assert.False(vm.IsVisible);
    }

    private class MockUpdateService : IUpdateService
    {
        public bool DownloadInvoked { get; private set; }
        public bool LaunchInvoked { get; private set; }

        public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "1.2.0",
                LatestVersion = "1.3.0",
                DownloadUrl = "https://example.com/setup.exe",
                AssetFileName = "setup.exe",
                AssetSize = 1000
            });
        }

        public Task<string> DownloadUpdateAsync(UpdateCheckResult updateInfo, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            DownloadInvoked = true;
            progress?.Report(new UpdateDownloadProgress(1000, 1000, 100, 500));
            return Task.FromResult(Path.Combine(Path.GetTempPath(), "mock-installer.exe"));
        }

        public bool LaunchInstallerAndExit(string installerPath, bool silent = false)
        {
            LaunchInvoked = true;
            return true;
        }

        public Task<bool> VerifyChecksumAsync(string filePath, string checksumsUrl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task UpdateModalViewModel_DownloadAndInstallAsync_TransitionsToDownloaded()
    {
        var mockService = new MockUpdateService();
        var vm = new UpdateModalViewModel(mockService);

        var checkResult = await mockService.CheckForUpdatesAsync();
        vm.ShowUpdate(checkResult);

        Assert.True(vm.CanStartDownload);

        await vm.DownloadAndInstallAsync();

        Assert.True(mockService.DownloadInvoked);
        Assert.False(vm.IsDownloading);
        Assert.True(vm.IsDownloaded);
        Assert.False(vm.CanStartDownload);
        Assert.Equal("Update Ready to Install", vm.ModalTitle);
        Assert.Equal(100, vm.DownloadProgress);

        vm.InstallAndRestart();
        Assert.True(mockService.LaunchInvoked);
    }
}
