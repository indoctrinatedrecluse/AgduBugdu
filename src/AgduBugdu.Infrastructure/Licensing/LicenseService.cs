using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.Core;
using AgduBugdu.Core.Licensing;

namespace AgduBugdu.Infrastructure.Licensing;

public class LicenseService : ILicenseService
{
    public const string DefaultLicensorUrl = "https://licensor-h5zdysrkqa-uc.a.run.app";
    public const string ProductId = "agdubugdu";

    private readonly ILicenseStorage _storage;
    private readonly HttpClient _httpClient;
    private readonly string _serverBaseUrl;
    private LicenseInfo _currentLicense;

    public LicenseInfo CurrentLicense => _currentLicense;
    public event EventHandler<LicenseInfo>? LicenseChanged;

    public LicenseService(ILicenseStorage? storage = null, HttpClient? httpClient = null, string? serverUrl = null)
    {
        _storage = storage ?? new RegistryLicenseStorage();
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _serverBaseUrl = (serverUrl ?? DefaultLicensorUrl).TrimEnd('/');

        if (httpClient == null)
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AgduBugdu-Client", AppVersionInfo.CurrentVersion));
        }

        _currentLicense = LoadInitialLicense();
    }

    public string GetMachineHWID()
    {
        string machine = Environment.MachineName;
        string os = RuntimeInformation.OSDescription;
        string arch = RuntimeInformation.ProcessArchitecture.ToString();
        string raw = $"{machine}-{os}-{arch}";

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    raw = $"{guid}-{machine}-{arch}";
                }
            }
            catch { }
        }

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..24].ToUpperInvariant();
    }

    public async Task<LicenseValidationResult> ActivateLicenseAsync(
        string licenseKey,
        string? username = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        string key = (licenseKey ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new LicenseValidationResult
            {
                Success = false,
                Message = "License key is required."
            };
        }

        var type = ParseLicenseType(key);
        if (type == LicenseType.Trial)
        {
            return new LicenseValidationResult
            {
                Success = false,
                Message = "Invalid key format. License keys must begin with ADM-, DEV-, or USER-."
            };
        }

        // Admin licenses do not require username/password; User and Dev licenses do.
        if (type != LicenseType.Admin)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new LicenseValidationResult
                {
                    Success = false,
                    Message = "Username is required for User and Dev licenses."
                };
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new LicenseValidationResult
                {
                    Success = false,
                    Message = "Password is required for User and Dev licenses."
                };
            }
        }

        string hwid = GetMachineHWID();
        string platform = $"{RuntimeInformation.OSDescription.ToLowerInvariant()}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";

        var payload = new
        {
            license_key = key,
            product_id = ProductId,
            username = username?.Trim() ?? string.Empty,
            password = password ?? string.Empty,
            hwid = hwid,
            machine_name = Environment.MachineName,
            platform = platform,
            app_version = AppVersionInfo.CurrentVersion
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_serverBaseUrl}/api/v1/license/activate", content, cancellationToken);

            var respJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errMsg = "Activation failed.";
                try
                {
                    using var doc = JsonDocument.Parse(respJson);
                    if (doc.RootElement.TryGetProperty("error", out var errProp))
                    {
                        errMsg = errProp.GetString() ?? errMsg;
                    }
                }
                catch { }

                return new LicenseValidationResult
                {
                    Success = false,
                    Message = errMsg
                };
            }

            // Success 200 OK
            DateTime? expiresAt = null;
            try
            {
                using var doc = JsonDocument.Parse(respJson);
                if (doc.RootElement.TryGetProperty("expires_at", out var expProp) &&
                    expProp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(expProp.GetString(), out var parsedExp))
                {
                    expiresAt = parsedExp;
                }
            }
            catch { }

            var record = new LicenseRecord
            {
                LicenseKey = key,
                Username = username?.Trim() ?? string.Empty,
                Type = type,
                Status = LicenseStatus.Active,
                ExpiresAt = expiresAt,
                HWID = hwid,
                LastValidatedAt = DateTime.UtcNow,
                TrialStartDate = _storage.GetOrCreateTrialStartDate()
            };

            _storage.SaveLicenseRecord(record);

            _currentLicense = new LicenseInfo
            {
                LicenseKey = key,
                Username = record.Username,
                Type = type,
                Status = LicenseStatus.Active,
                ExpiresAt = expiresAt,
                HWID = hwid,
                LastValidatedAt = DateTime.UtcNow,
                TrialStartDate = record.TrialStartDate
            };

            LicenseChanged?.Invoke(this, _currentLicense);

            return new LicenseValidationResult
            {
                Success = true,
                Message = $"Successfully activated {CurrentLicense.DisplayTitle}!",
                License = _currentLicense
            };
        }
        catch (Exception ex)
        {
            return new LicenseValidationResult
            {
                Success = false,
                Message = $"Network error communicating with license authority: {ex.Message}"
            };
        }
    }

    public async Task<LicenseValidationResult> ValidateCurrentLicenseAsync(CancellationToken cancellationToken = default)
    {
        if (_currentLicense.Type == LicenseType.Trial)
        {
            var trialStart = _storage.GetOrCreateTrialStartDate();
            _currentLicense.TrialStartDate = trialStart;
            var elapsedDays = (DateTime.UtcNow - trialStart).TotalDays;

            if (elapsedDays > 60)
            {
                _currentLicense.Status = LicenseStatus.TrialExpired;
                LicenseChanged?.Invoke(this, _currentLicense);
                return new LicenseValidationResult
                {
                    Success = false,
                    Message = "Trial period has expired (60 days elapsed).",
                    License = _currentLicense
                };
            }

            _currentLicense.Status = LicenseStatus.TrialActive;
            return new LicenseValidationResult
            {
                Success = true,
                Message = $"{_currentLicense.DaysRemaining} days remaining in trial.",
                License = _currentLicense
            };
        }

        // Paid license validation
        string hwid = GetMachineHWID();
        var payload = new
        {
            license_key = _currentLicense.LicenseKey,
            product_id = ProductId,
            hwid = hwid,
            username = _currentLicense.Username
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_serverBaseUrl}/api/v1/license/validate", content, cancellationToken);
            var respJson = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(respJson);
            bool isValid = doc.RootElement.TryGetProperty("valid", out var v) && v.GetBoolean();
            string statusStr = doc.RootElement.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
            string msg = doc.RootElement.TryGetProperty("message", out var m) ? (m.GetString() ?? "") : "";

            if (isValid)
            {
                DateTime? expiresAt = null;
                if (doc.RootElement.TryGetProperty("expires_at", out var exp) &&
                    exp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(exp.GetString(), out var pExp))
                {
                    expiresAt = pExp;
                }

                int? graceDays = doc.RootElement.TryGetProperty("grace_days_left", out var g) ? g.GetInt32() : null;

                _currentLicense.Status = LicenseStatus.Active;
                _currentLicense.ExpiresAt = expiresAt;
                _currentLicense.GraceDaysLeft = graceDays;
                _currentLicense.LastValidatedAt = DateTime.UtcNow;

                _storage.SaveLicenseRecord(new LicenseRecord
                {
                    LicenseKey = _currentLicense.LicenseKey,
                    Username = _currentLicense.Username,
                    Type = _currentLicense.Type,
                    Status = LicenseStatus.Active,
                    ExpiresAt = expiresAt,
                    HWID = hwid,
                    LastValidatedAt = DateTime.UtcNow,
                    TrialStartDate = _currentLicense.TrialStartDate
                });

                LicenseChanged?.Invoke(this, _currentLicense);

                return new LicenseValidationResult
                {
                    Success = true,
                    Message = "License validated successfully.",
                    License = _currentLicense
                };
            }
            else
            {
                // Invalid response from server
                _currentLicense.Status = statusStr.ToLowerInvariant() switch
                {
                    "expired" => LicenseStatus.Expired,
                    "suspended" => LicenseStatus.Suspended,
                    "revoked" => LicenseStatus.Revoked,
                    "hwid_unregistered" => LicenseStatus.Unregistered,
                    _ => LicenseStatus.Invalid
                };

                LicenseChanged?.Invoke(this, _currentLicense);

                return new LicenseValidationResult
                {
                    Success = false,
                    Message = string.IsNullOrWhiteSpace(msg) ? $"License validation rejected ({statusStr})." : msg,
                    License = _currentLicense
                };
            }
        }
        catch (Exception ex)
        {
            // Offline fallback: Check last validated date within 30-day grace period
            if (_currentLicense.LastValidatedAt.HasValue &&
                (DateTime.UtcNow - _currentLicense.LastValidatedAt.Value).TotalDays <= 30)
            {
                return new LicenseValidationResult
                {
                    Success = true,
                    Message = "Validated offline (cached lease active).",
                    License = _currentLicense
                };
            }

            return new LicenseValidationResult
            {
                Success = false,
                Message = $"Unable to contact license server: {ex.Message}",
                License = _currentLicense
            };
        }
    }

    public async Task<bool> DeactivateAndRevertToTrialAsync(
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        if (_currentLicense.IsPaid && !string.IsNullOrWhiteSpace(_currentLicense.LicenseKey))
        {
            try
            {
                var payload = new
                {
                    license_key = _currentLicense.LicenseKey,
                    product_id = ProductId,
                    hwid = GetMachineHWID(),
                    username = _currentLicense.Username,
                    password = password ?? string.Empty
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"{_serverBaseUrl}/api/v1/license/deactivate", content, cancellationToken);
            }
            catch
            {
                // Ignore network error on deactivation cleanup
            }
        }

        RevertToTrial();
        return true;
    }

    public void RevertToTrial()
    {
        _storage.ClearLicenseRecord();
        var trialStart = _storage.GetOrCreateTrialStartDate();
        var elapsedDays = (DateTime.UtcNow - trialStart).TotalDays;

        _currentLicense = new LicenseInfo
        {
            LicenseKey = string.Empty,
            Username = string.Empty,
            Type = LicenseType.Trial,
            Status = elapsedDays > 60 ? LicenseStatus.TrialExpired : LicenseStatus.TrialActive,
            TrialStartDate = trialStart,
            HWID = GetMachineHWID()
        };

        LicenseChanged?.Invoke(this, _currentLicense);
    }

    private LicenseInfo LoadInitialLicense()
    {
        var record = _storage.LoadLicenseRecord();
        var trialStart = _storage.GetOrCreateTrialStartDate();

        if (record != null && !string.IsNullOrWhiteSpace(record.LicenseKey) && record.Type != LicenseType.Trial)
        {
            return new LicenseInfo
            {
                LicenseKey = record.LicenseKey,
                Username = record.Username,
                Type = record.Type,
                Status = record.Status,
                ExpiresAt = record.ExpiresAt,
                TrialStartDate = trialStart,
                LastValidatedAt = record.LastValidatedAt,
                HWID = record.HWID
            };
        }

        var elapsedDays = (DateTime.UtcNow - trialStart).TotalDays;
        return new LicenseInfo
        {
            LicenseKey = string.Empty,
            Username = string.Empty,
            Type = LicenseType.Trial,
            Status = elapsedDays > 60 ? LicenseStatus.TrialExpired : LicenseStatus.TrialActive,
            TrialStartDate = trialStart,
            HWID = GetMachineHWID()
        };
    }

    public static LicenseType ParseLicenseType(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return LicenseType.Trial;

        var upper = key.Trim().ToUpperInvariant();
        if (upper.StartsWith("ADM-")) return LicenseType.Admin;
        if (upper.StartsWith("DEV-")) return LicenseType.Dev;
        if (upper.StartsWith("USER-")) return LicenseType.User;

        return LicenseType.Trial;
    }
}
