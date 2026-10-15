using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgduBugdu.Core;
using AgduBugdu.Core.Licensing;
using Avalonia.Controls.ApplicationLifetimes;

namespace AgduBugdu.App.ViewModels;

public partial class AboutModalViewModel : ViewModelBase
{
    private readonly ILicenseService? _licenseService;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _copyLicenseStatus = "Copy License Text";

    [ObservableProperty]
    private string _copyPortfolioStatus = "Copy Link";

    public string AppName => AppVersionInfo.AppName;
    public string Version => AppVersionInfo.CurrentVersion;
    public string AuthorName => "indoctrinatedrecluse aka Abhishek Mitra";
    public string PortfolioUrl => "https://portfolio-flutter-78bcf.web.app/";
    public string LicenseType => "MIT License";

    public string EntitlementTitle => _licenseService?.CurrentLicense.DisplayTitle ?? "Community Edition";
    public string EntitlementBadge => $"{_licenseService?.CurrentLicense.BadgeIcon ?? "⏳"} {EntitlementTitle}";
    public string FormattedValidity => _licenseService?.CurrentLicense.FormattedValidity ?? "Active (Open Source)";

    public string LicenseBadgeBackground => (_licenseService?.CurrentLicense.Type) switch
    {
        Core.Licensing.LicenseType.Admin => "#78350F",
        Core.Licensing.LicenseType.Dev => "#1E3A8A",
        Core.Licensing.LicenseType.User => "#065F46",
        _ => "#334155"
    };

    public string LicenseBadgeBorder => (_licenseService?.CurrentLicense.Type) switch
    {
        Core.Licensing.LicenseType.Admin => "#F59E0B",
        Core.Licensing.LicenseType.Dev => "#38BDF8",
        Core.Licensing.LicenseType.User => "#34D399",
        _ => "#64748B"
    };

    public string LicenseBadgeForeground => (_licenseService?.CurrentLicense.Type) switch
    {
        Core.Licensing.LicenseType.Admin => "#FEF3C7",
        Core.Licensing.LicenseType.Dev => "#E0F2FE",
        Core.Licensing.LicenseType.User => "#D1FAE5",
        _ => "#CBD5E1"
    };

    public const string FullLicenseText =
@"MIT License

Copyright (c) 2026 indoctrinatedrecluse

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";

    public string LicenseNotice => FullLicenseText;

    public AboutModalViewModel(ILicenseService? licenseService = null)
    {
        _licenseService = licenseService;
        if (_licenseService != null)
        {
            _licenseService.LicenseChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(EntitlementTitle));
                OnPropertyChanged(nameof(EntitlementBadge));
                OnPropertyChanged(nameof(FormattedValidity));
                OnPropertyChanged(nameof(LicenseBadgeBackground));
                OnPropertyChanged(nameof(LicenseBadgeBorder));
                OnPropertyChanged(nameof(LicenseBadgeForeground));
            };
        }
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
    public async Task CopyLicenseAsync()
    {
        await SetClipboardTextAsync(FullLicenseText);
        CopyLicenseStatus = "✓ Copied to Clipboard!";
        _ = Task.Delay(2500).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CopyLicenseStatus = "Copy License Text");
        });
    }

    [RelayCommand]
    public async Task CopyPortfolioUrlAsync()
    {
        await SetClipboardTextAsync(PortfolioUrl);
        CopyPortfolioStatus = "✓ Copied!";
        _ = Task.Delay(2500).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CopyPortfolioStatus = "Copy Link");
        });
    }

    [RelayCommand]
    public void OpenPortfolio()
    {
        OpenUrl(PortfolioUrl);
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
        }
        catch { }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch { }
    }
}
