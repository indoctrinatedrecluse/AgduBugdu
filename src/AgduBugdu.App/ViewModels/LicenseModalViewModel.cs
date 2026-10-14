using System;
using System.Threading.Tasks;
using AgduBugdu.Core.Licensing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgduBugdu.App.ViewModels;

public partial class LicenseModalViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _licenseKeyInput = string.Empty;

    [ObservableProperty]
    private string _usernameInput = string.Empty;

    [ObservableProperty]
    private string _passwordInput = string.Empty;

    [ObservableProperty]
    private bool _isRevealed;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    public char LicensePasswordChar => IsRevealed ? '\0' : '●';
    public string RevealButtonText => IsRevealed ? "Hide" : "Reveal";

    public LicenseInfo CurrentLicense => _licenseService.CurrentLicense;

    public string MachineHwid => _licenseService.GetMachineHWID();

    public bool IsAdminKey => LicenseKeyInput.Trim().StartsWith("ADM-", StringComparison.OrdinalIgnoreCase);

    public bool IsCredentialsRequired => !IsAdminKey && (
        LicenseKeyInput.Trim().StartsWith("USER-", StringComparison.OrdinalIgnoreCase) ||
        LicenseKeyInput.Trim().StartsWith("DEV-", StringComparison.OrdinalIgnoreCase));

    public bool CanRemoveLicense => CurrentLicense.IsPaid;

    public bool IsAdminBadge => CurrentLicense.Type == LicenseType.Admin;
    public bool IsDevBadge => CurrentLicense.Type == LicenseType.Dev;
    public bool IsUserBadge => CurrentLicense.Type == LicenseType.User;
    public bool IsTrialBadge => CurrentLicense.Type == LicenseType.Trial;

    public LicenseModalViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _licenseService.LicenseChanged += OnLicenseChanged;
    }

    private void OnLicenseChanged(object? sender, LicenseInfo e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshProperties();
        });
    }

    partial void OnIsRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(LicensePasswordChar));
        OnPropertyChanged(nameof(RevealButtonText));
    }

    partial void OnLicenseKeyInputChanged(string value)
    {
        OnPropertyChanged(nameof(IsAdminKey));
        OnPropertyChanged(nameof(IsCredentialsRequired));
    }

    public void Show()
    {
        RefreshProperties();
        StatusMessage = string.Empty;
        IsErrorMessage = false;
        IsVisible = true;
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(CurrentLicense));
        OnPropertyChanged(nameof(CanRemoveLicense));
        OnPropertyChanged(nameof(MachineHwid));
        OnPropertyChanged(nameof(IsAdminBadge));
        OnPropertyChanged(nameof(IsDevBadge));
        OnPropertyChanged(nameof(IsUserBadge));
        OnPropertyChanged(nameof(IsTrialBadge));
    }

    [RelayCommand]
    public void ToggleReveal()
    {
        IsRevealed = !IsRevealed;
    }

    [RelayCommand]
    public async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKeyInput))
        {
            StatusMessage = "Please enter a valid license key.";
            IsErrorMessage = true;
            return;
        }

        IsBusy = true;
        StatusMessage = "Validating with license authority...";
        IsErrorMessage = false;

        try
        {
            var result = await _licenseService.ActivateLicenseAsync(
                LicenseKeyInput,
                UsernameInput,
                PasswordInput);

            if (result.Success)
            {
                StatusMessage = result.Message;
                IsErrorMessage = false;
                LicenseKeyInput = string.Empty;
                UsernameInput = string.Empty;
                PasswordInput = string.Empty;
                RefreshProperties();
            }
            else
            {
                StatusMessage = result.Message;
                IsErrorMessage = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Activation error: {ex.Message}";
            IsErrorMessage = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RemoveLicenseAsync()
    {
        if (!CurrentLicense.IsPaid) return;

        IsBusy = true;
        StatusMessage = "Reverting to local Trial mode...";
        IsErrorMessage = false;

        try
        {
            await _licenseService.DeactivateAndRevertToTrialAsync(PasswordInput);
            StatusMessage = "Reverted to 60-day Trial license.";
            IsErrorMessage = false;
            LicenseKeyInput = string.Empty;
            UsernameInput = string.Empty;
            PasswordInput = string.Empty;
            RefreshProperties();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing license: {ex.Message}";
            IsErrorMessage = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
    }
}
