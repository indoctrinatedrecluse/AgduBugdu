using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Licensing;

public interface ILicenseService : IDisposable
{
    LicenseInfo CurrentLicense { get; }
    event EventHandler<LicenseInfo>? LicenseChanged;

    string GetMachineHWID();

    Task<LicenseValidationResult> ActivateLicenseAsync(
        string licenseKey,
        string? username = null,
        string? password = null,
        CancellationToken cancellationToken = default);

    Task<LicenseValidationResult> ValidateCurrentLicenseAsync(
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAndRevertToTrialAsync(
        string? password = null,
        CancellationToken cancellationToken = default);

    void RevertToTrial();
}
