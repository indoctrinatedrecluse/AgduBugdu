using System;
using System.IO;
using System.Text.Json;
using AgduBugdu.Core.Licensing;
using Microsoft.Win32;

namespace AgduBugdu.Infrastructure.Licensing;

public class RegistryLicenseStorage : ILicenseStorage
{
    private const string RegistryKeyPath = @"Software\AgduBugdu\License";
    private readonly string _fallbackFilePath;

    public RegistryLicenseStorage()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(appData, ".agdubugdu");
        Directory.CreateDirectory(dir);
        _fallbackFilePath = Path.Combine(dir, "license.json");
    }

    public LicenseRecord? LoadLicenseRecord()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                if (key == null) return null;

                var licenseKey = key.GetValue("LicenseKey")?.ToString();
                if (string.IsNullOrWhiteSpace(licenseKey)) return null;

                var username = key.GetValue("Username")?.ToString() ?? string.Empty;
                var typeStr = key.GetValue("LicenseType")?.ToString() ?? "Trial";
                var statusStr = key.GetValue("Status")?.ToString() ?? "Active";
                var expiresAtStr = key.GetValue("ExpiresAt")?.ToString();
                var trialStartStr = key.GetValue("TrialStartDate")?.ToString();
                var lastValidatedStr = key.GetValue("LastValidatedAt")?.ToString();
                var hwid = key.GetValue("HWID")?.ToString() ?? string.Empty;

                Enum.TryParse<LicenseType>(typeStr, true, out var type);
                Enum.TryParse<LicenseStatus>(statusStr, true, out var status);

                DateTime? expiresAt = DateTime.TryParse(expiresAtStr, out var exp) ? exp : null;
                DateTime? trialStart = DateTime.TryParse(trialStartStr, out var tStart) ? tStart : null;
                DateTime? lastValidated = DateTime.TryParse(lastValidatedStr, out var lVal) ? lVal : null;

                return new LicenseRecord
                {
                    LicenseKey = licenseKey,
                    Username = username,
                    Type = type,
                    Status = status,
                    ExpiresAt = expiresAt,
                    TrialStartDate = trialStart,
                    LastValidatedAt = lastValidated,
                    HWID = hwid
                };
            }
            catch
            {
                // Fall back to disk storage on permission issues
            }
        }

        // Cross-platform / fallback disk storage
        if (File.Exists(_fallbackFilePath))
        {
            try
            {
                var json = File.ReadAllText(_fallbackFilePath);
                return JsonSerializer.Deserialize<LicenseRecord>(json);
            }
            catch { }
        }

        return null;
    }

    public void SaveLicenseRecord(LicenseRecord record)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                if (key != null)
                {
                    key.SetValue("LicenseKey", record.LicenseKey);
                    key.SetValue("Username", record.Username);
                    key.SetValue("LicenseType", record.Type.ToString());
                    key.SetValue("Status", record.Status.ToString());
                    key.SetValue("HWID", record.HWID);

                    if (record.ExpiresAt.HasValue)
                        key.SetValue("ExpiresAt", record.ExpiresAt.Value.ToString("O"));
                    else
                        key.DeleteValue("ExpiresAt", false);

                    if (record.TrialStartDate.HasValue)
                        key.SetValue("TrialStartDate", record.TrialStartDate.Value.ToString("O"));

                    if (record.LastValidatedAt.HasValue)
                        key.SetValue("LastValidatedAt", record.LastValidatedAt.Value.ToString("O"));

                    return;
                }
            }
            catch
            {
                // Fall back to file
            }
        }

        // Fallback file storage
        try
        {
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_fallbackFilePath, json);
        }
        catch { }
    }

    public void ClearLicenseRecord()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key != null)
                {
                    // Clear paid license values while preserving TrialStartDate
                    key.DeleteValue("LicenseKey", false);
                    key.DeleteValue("Username", false);
                    key.SetValue("LicenseType", LicenseType.Trial.ToString());
                    key.SetValue("Status", LicenseStatus.TrialActive.ToString());
                    key.DeleteValue("ExpiresAt", false);
                    key.DeleteValue("LastValidatedAt", false);
                }
            }
            catch { }
        }

        if (File.Exists(_fallbackFilePath))
        {
            try
            {
                var trialStart = GetOrCreateTrialStartDate();
                var fallback = new LicenseRecord
                {
                    LicenseKey = string.Empty,
                    Type = LicenseType.Trial,
                    Status = LicenseStatus.TrialActive,
                    TrialStartDate = trialStart
                };
                File.WriteAllText(_fallbackFilePath, JsonSerializer.Serialize(fallback));
            }
            catch { }
        }
    }

    public DateTime GetOrCreateTrialStartDate()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                if (key != null)
                {
                    var existing = key.GetValue("TrialStartDate")?.ToString();
                    if (!string.IsNullOrWhiteSpace(existing) && DateTime.TryParse(existing, out var dt))
                    {
                        return dt;
                    }

                    var now = DateTime.UtcNow;
                    key.SetValue("TrialStartDate", now.ToString("O"));
                    return now;
                }
            }
            catch { }
        }

        // Fallback file
        try
        {
            if (File.Exists(_fallbackFilePath))
            {
                var json = File.ReadAllText(_fallbackFilePath);
                var rec = JsonSerializer.Deserialize<LicenseRecord>(json);
                if (rec?.TrialStartDate.HasValue == true)
                {
                    return rec.TrialStartDate.Value;
                }
            }

            var now = DateTime.UtcNow;
            var newRec = new LicenseRecord { TrialStartDate = now };
            File.WriteAllText(_fallbackFilePath, JsonSerializer.Serialize(newRec));
            return now;
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }
}
