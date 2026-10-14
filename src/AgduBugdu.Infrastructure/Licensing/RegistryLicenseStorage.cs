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
    private readonly object _lock = new();

    private readonly bool _useRegistry;

    public RegistryLicenseStorage(string? customFallbackPath = null, bool? useRegistry = null)
    {
        _useRegistry = useRegistry ?? string.IsNullOrEmpty(customFallbackPath);
        if (!string.IsNullOrEmpty(customFallbackPath))
        {
            _fallbackFilePath = customFallbackPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(appData, ".agdubugdu");
            Directory.CreateDirectory(dir);
            _fallbackFilePath = Path.Combine(dir, "license.json");
        }
    }

    public LicenseRecord? LoadLicenseRecord()
    {
        lock (_lock)
        {
            LicenseRecord? record = null;

            if (_useRegistry && OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                    if (key != null)
                    {
                        var licenseKey = key.GetValue("LicenseKey")?.ToString();
                        if (!string.IsNullOrWhiteSpace(licenseKey))
                        {
                            var username = key.GetValue("Username")?.ToString() ?? string.Empty;
                            var typeStr = key.GetValue("LicenseType")?.ToString() ?? "Trial";
                            var statusStr = key.GetValue("Status")?.ToString() ?? "Active";
                            var expiresAtStr = key.GetValue("ExpiresAt")?.ToString();
                            var trialStartStr = key.GetValue("TrialStartDate")?.ToString();
                            var lastValidatedStr = key.GetValue("LastValidatedAt")?.ToString();
                            var hwid = key.GetValue("HWID")?.ToString() ?? string.Empty;
                            var encLease = key.GetValue("EncryptedLease")?.ToString();
                            var hmac = key.GetValue("IntegrityHmac")?.ToString();
                            var clockTicksStr = key.GetValue("LastKnownClockTicks")?.ToString();

                            Enum.TryParse<LicenseType>(typeStr, true, out var type);
                            Enum.TryParse<LicenseStatus>(statusStr, true, out var status);

                            DateTime? expiresAt = DateTime.TryParse(expiresAtStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var exp) ? exp : null;
                            DateTime? trialStart = DateTime.TryParse(trialStartStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var tStart) ? tStart : null;
                            DateTime? lastValidated = DateTime.TryParse(lastValidatedStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var lVal) ? lVal : null;
                            long.TryParse(clockTicksStr, out var clockTicks);

                            record = new LicenseRecord
                            {
                                LicenseKey = licenseKey,
                                Username = username,
                                Type = type,
                                Status = status,
                                ExpiresAt = expiresAt,
                                TrialStartDate = trialStart,
                                LastValidatedAt = lastValidated,
                                HWID = hwid,
                                EncryptedLease = encLease,
                                IntegrityHmac = hmac,
                                LastKnownClockTicks = clockTicks
                            };
                        }
                    }
                }
                catch
                {
                    // Fall back to file
                }
            }

            // Cross-platform / fallback disk storage if registry didn't return record
            if (record == null && File.Exists(_fallbackFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_fallbackFilePath);
                    record = JsonSerializer.Deserialize<LicenseRecord>(json);
                }
                catch { }
            }

            if (record == null || string.IsNullOrWhiteSpace(record.LicenseKey) || record.Type == LicenseType.Trial)
            {
                return record;
            }

            // Verify HMAC integrity for paid license record
            var machineKey = LicenseSecurity.DeriveMachineKey(record.HWID);
            if (!LicenseSecurity.VerifyIntegrityHmac(record, machineKey))
            {
                // Tampering detected: Corrupted or manually modified registry keys
                return null;
            }

            return record;
        }
    }

    public void SaveLicenseRecord(LicenseRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_lock)
        {
            // Ensure monotonic clock high-water mark
            long currentHighWater = GetLastKnownClockTicks();
            record.LastKnownClockTicks = Math.Max(record.LastKnownClockTicks, currentHighWater);
            record.LastKnownClockTicks = Math.Max(record.LastKnownClockTicks, DateTime.UtcNow.Ticks);

            // Compute HMAC integrity signature
            var machineKey = LicenseSecurity.DeriveMachineKey(record.HWID);
            record.IntegrityHmac = LicenseSecurity.ComputeIntegrityHmac(record, machineKey);

            if (_useRegistry && OperatingSystem.IsWindows())
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
                        key.SetValue("LastKnownClockTicks", record.LastKnownClockTicks.ToString());

                        if (!string.IsNullOrEmpty(record.EncryptedLease))
                            key.SetValue("EncryptedLease", record.EncryptedLease);
                        else
                            key.DeleteValue("EncryptedLease", false);

                        if (!string.IsNullOrEmpty(record.IntegrityHmac))
                            key.SetValue("IntegrityHmac", record.IntegrityHmac);
                        else
                            key.DeleteValue("IntegrityHmac", false);

                        if (record.ExpiresAt.HasValue)
                            key.SetValue("ExpiresAt", record.ExpiresAt.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
                        else
                            key.DeleteValue("ExpiresAt", false);

                        if (record.TrialStartDate.HasValue)
                            key.SetValue("TrialStartDate", record.TrialStartDate.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));

                        if (record.LastValidatedAt.HasValue)
                            key.SetValue("LastValidatedAt", record.LastValidatedAt.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));

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
                var dir = Path.GetDirectoryName(_fallbackFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_fallbackFilePath, json);
            }
            catch { }
        }
    }

    public void ClearLicenseRecord()
    {
        lock (_lock)
        {
            if (_useRegistry && OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                    if (key != null)
                    {
                        key.DeleteValue("LicenseKey", false);
                        key.DeleteValue("Username", false);
                        key.SetValue("LicenseType", LicenseType.Trial.ToString());
                        key.SetValue("Status", LicenseStatus.TrialActive.ToString());
                        key.DeleteValue("ExpiresAt", false);
                        key.DeleteValue("LastValidatedAt", false);
                        key.DeleteValue("EncryptedLease", false);
                        key.DeleteValue("IntegrityHmac", false);
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
                        TrialStartDate = trialStart,
                        LastKnownClockTicks = GetLastKnownClockTicks()
                    };
                    File.WriteAllText(_fallbackFilePath, JsonSerializer.Serialize(fallback));
                }
                catch { }
            }
        }
    }

    public DateTime GetOrCreateTrialStartDate()
    {
        lock (_lock)
        {
            if (_useRegistry && OperatingSystem.IsWindows())
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

                    var now = DateTime.UtcNow;
                    rec ??= new LicenseRecord();
                    rec.TrialStartDate = now;
                    rec.LastKnownClockTicks = Math.Max(rec.LastKnownClockTicks, now.Ticks);
                    File.WriteAllText(_fallbackFilePath, JsonSerializer.Serialize(rec, new JsonSerializerOptions { WriteIndented = true }));
                    return now;
                }

                var nowNew = DateTime.UtcNow;
                long curTicks = Math.Max(GetLastKnownClockTicks(), nowNew.Ticks);
                var newRec = new LicenseRecord { TrialStartDate = nowNew, LastKnownClockTicks = curTicks };
                File.WriteAllText(_fallbackFilePath, JsonSerializer.Serialize(newRec, new JsonSerializerOptions { WriteIndented = true }));
                return nowNew;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }

    public long GetLastKnownClockTicks()
    {
        lock (_lock)
        {
            if (_useRegistry && OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                    if (key != null)
                    {
                        var val = key.GetValue("LastKnownClockTicks")?.ToString();
                        if (long.TryParse(val, out var ticks))
                        {
                            return ticks;
                        }
                    }
                }
                catch { }
            }

            try
            {
                if (File.Exists(_fallbackFilePath))
                {
                    var json = File.ReadAllText(_fallbackFilePath);
                    var rec = JsonSerializer.Deserialize<LicenseRecord>(json);
                    if (rec != null && rec.LastKnownClockTicks > 0)
                    {
                        return rec.LastKnownClockTicks;
                    }
                }
            }
            catch { }

            return 0;
        }
    }

    public void UpdateLastKnownClockTicks(long ticks)
    {
        lock (_lock)
        {
            long current = GetLastKnownClockTicks();
            if (ticks <= current)
                return;

            if (_useRegistry && OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                    key?.SetValue("LastKnownClockTicks", ticks.ToString());
                }
                catch { }
            }

            try
            {
                var dir = Path.GetDirectoryName(_fallbackFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var rec = File.Exists(_fallbackFilePath)
                    ? JsonSerializer.Deserialize<LicenseRecord>(File.ReadAllText(_fallbackFilePath)) ?? new LicenseRecord()
                    : new LicenseRecord();

                rec.LastKnownClockTicks = ticks;
                File.WriteAllText(_fallbackFilePath, JsonSerializer.Serialize(rec));
            }
            catch { }
        }
    }
}
