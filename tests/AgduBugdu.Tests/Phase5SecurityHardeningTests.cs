using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.Core.Licensing;
using AgduBugdu.Infrastructure.Licensing;
using Xunit;

namespace AgduBugdu.Tests;

public class Phase5SecurityHardeningTests : IDisposable
{
    private readonly string _tempStoragePath;

    public Phase5SecurityHardeningTests()
    {
        _tempStoragePath = Path.Combine(Path.GetTempPath(), $"agdubugdu_security_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempStoragePath))
            {
                File.Delete(_tempStoragePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void AES256GCM_EncryptAndDecrypt_RoundTripPreservesAllFields()
    {
        string hwid = "TEST-HWID-0123456789AB";
        byte[] machineKey = LicenseSecurity.DeriveMachineKey(hwid);

        var originalLease = new OfflineLeasePayload
        {
            LicenseKey = "ADM-1111-2222-3333",
            Username = "EnterpriseAdmin",
            Type = LicenseType.Admin,
            HWID = hwid,
            LeaseIssuedAtUtc = DateTime.UtcNow,
            OfflineGraceHardLimitUtc = DateTime.UtcNow.AddDays(7),
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            MonotonicSequence = 42
        };

        string encryptedBase64 = LicenseSecurity.EncryptLease(originalLease, machineKey);
        Assert.False(string.IsNullOrWhiteSpace(encryptedBase64));

        var decryptedLease = LicenseSecurity.DecryptLease(encryptedBase64, machineKey);
        Assert.NotNull(decryptedLease);
        Assert.Equal(originalLease.LicenseKey, decryptedLease.LicenseKey);
        Assert.Equal(originalLease.Username, decryptedLease.Username);
        Assert.Equal(originalLease.Type, decryptedLease.Type);
        Assert.Equal(originalLease.HWID, decryptedLease.HWID);
        Assert.Equal(originalLease.MonotonicSequence, decryptedLease.MonotonicSequence);
        Assert.Equal(originalLease.ExpiresAtUtc?.Date, decryptedLease.ExpiresAtUtc?.Date);
    }

    [Fact]
    public void AES256GCM_MachineCloningPrevention_ThrowsOnDifferentMachineKey()
    {
        string machineAHwid = "HWID-MACHINE-AAA-111";
        string machineBHwid = "HWID-MACHINE-BBB-222";

        byte[] machineAKey = LicenseSecurity.DeriveMachineKey(machineAHwid);
        byte[] machineBKey = LicenseSecurity.DeriveMachineKey(machineBHwid);

        var lease = new OfflineLeasePayload
        {
            LicenseKey = "DEV-9999-8888-7777",
            Username = "DevUser",
            Type = LicenseType.Dev,
            HWID = machineAHwid,
            LeaseIssuedAtUtc = DateTime.UtcNow,
            OfflineGraceHardLimitUtc = DateTime.UtcNow.AddDays(7)
        };

        // Encrypt on Machine A
        string encryptedOnMachineA = LicenseSecurity.EncryptLease(lease, machineAKey);

        // Attempt to decrypt on Machine B (e.g. copied to another PC)
        Assert.ThrowsAny<CryptographicException>(() =>
        {
            LicenseSecurity.DecryptLease(encryptedOnMachineA, machineBKey);
        });
    }

    [Fact]
    public void HMAC_TamperDetection_CatchesModifiedRegistryFields()
    {
        string hwid = "HWID-TAMPER-TEST-123";
        byte[] machineKey = LicenseSecurity.DeriveMachineKey(hwid);

        var initialExpires = DateTime.UtcNow.AddMonths(1);
        var record = new LicenseRecord
        {
            LicenseKey = "USER-AAAA-BBBB-CCCC",
            Username = "HonestUser",
            Type = LicenseType.User,
            Status = LicenseStatus.Active,
            HWID = hwid,
            ExpiresAt = initialExpires,
            TrialStartDate = DateTime.UtcNow.AddDays(-10),
            LastValidatedAt = DateTime.UtcNow,
            LastKnownClockTicks = DateTime.UtcNow.Ticks,
            EncryptedLease = "ValidCiphertextMock"
        };

        record.IntegrityHmac = LicenseSecurity.ComputeIntegrityHmac(record, machineKey);
        Assert.True(LicenseSecurity.VerifyIntegrityHmac(record, machineKey));

        // 1. Attacker tries elevating Type from User to Admin
        record.Type = LicenseType.Admin;
        Assert.False(LicenseSecurity.VerifyIntegrityHmac(record, machineKey));
        record.Type = LicenseType.User;

        // 2. Attacker tries changing ExpiresAt to year 2099
        record.ExpiresAt = DateTime.UtcNow.AddYears(50);
        Assert.False(LicenseSecurity.VerifyIntegrityHmac(record, machineKey));
        record.ExpiresAt = initialExpires;

        // 3. Attacker tries altering LicenseKey
        record.LicenseKey = "ADM-CRACKED-KEY-0000";
        Assert.False(LicenseSecurity.VerifyIntegrityHmac(record, machineKey));
        record.LicenseKey = "USER-AAAA-BBBB-CCCC";

        // 4. Attacker tries modifying EncryptedLease
        record.EncryptedLease = "TamperedCiphertext";
        Assert.False(LicenseSecurity.VerifyIntegrityHmac(record, machineKey));
        record.EncryptedLease = "ValidCiphertextMock";

        // Restored record verifies again
        Assert.True(LicenseSecurity.VerifyIntegrityHmac(record, machineKey));
    }

    [Fact]
    public void MonotonicClockGuard_DetectsClockRollbackAccurately()
    {
        var now = DateTime.UtcNow;
        long lastKnownTicks = now.Ticks;

        // Normal flow (time moving forward)
        bool rollback1 = LicenseSecurity.IsClockRollbackDetected(now.AddMinutes(1).Ticks, lastKnownTicks, out _);
        Assert.False(rollback1);

        // Minor clock skew (< 5 minutes backwards, acceptable NTP adjustment)
        bool rollbackSkew = LicenseSecurity.IsClockRollbackDetected(now.AddMinutes(-2).Ticks, lastKnownTicks, out _);
        Assert.False(rollbackSkew);

        // Real clock manipulation (> 5 minutes backwards, e.g. rolling back 1 hour)
        bool rollbackTamper = LicenseSecurity.IsClockRollbackDetected(now.AddHours(-1).Ticks, lastKnownTicks, out var duration);
        Assert.True(rollbackTamper);
        Assert.True(duration.TotalMinutes >= 55);
    }

    [Fact]
    public async Task LicenseService_OfflineGrace_ValidWithin7Days()
    {
        var storage = new RegistryLicenseStorage(_tempStoragePath, useRegistry: false);
        var failingHandler = new MockOfflineHttpMessageHandler();
        var httpClient = new HttpClient(failingHandler);

        var tempService = new LicenseService(storage, httpClient, enableTimers: false);
        string hwid = tempService.GetMachineHWID();
        byte[] machineKey = LicenseSecurity.DeriveMachineKey(hwid);

        var issuedAt = DateTime.UtcNow.AddDays(-2);
        var hardLimit = issuedAt.AddDays(7);

        var lease = new OfflineLeasePayload
        {
            LicenseKey = "ADM-TEST-7DAY-GRACE",
            Username = "AdminUser",
            Type = LicenseType.Admin,
            HWID = hwid,
            LeaseIssuedAtUtc = issuedAt,
            OfflineGraceHardLimitUtc = hardLimit,
            ExpiresAtUtc = DateTime.UtcNow.AddMonths(6)
        };

        var record = new LicenseRecord
        {
            LicenseKey = lease.LicenseKey,
            Username = lease.Username,
            Type = lease.Type,
            Status = LicenseStatus.Active,
            HWID = hwid,
            LastValidatedAt = issuedAt,
            ExpiresAt = lease.ExpiresAtUtc,
            TrialStartDate = DateTime.UtcNow.AddDays(-20),
            EncryptedLease = LicenseSecurity.EncryptLease(lease, machineKey),
            LastKnownClockTicks = issuedAt.Ticks
        };

        storage.SaveLicenseRecord(record);

        // Recreate service so it loads the saved active record
        var service = new LicenseService(storage, httpClient, enableTimers: false);
        var result = await service.ValidateCurrentLicenseAsync();

        Assert.True(result.Success, "Validation failed with message: " + result.Message);
        Assert.NotNull(result.License);
        Assert.Equal(LicenseStatus.Active, result.License.Status);
        Assert.Contains("Validated offline", result.Message);
        Assert.Contains("days remaining in secure 7-day offline grace", result.Message);
    }

    [Fact]
    public async Task LicenseService_OfflineGrace_ExpiredAfter7Days()
    {
        var storage = new RegistryLicenseStorage(_tempStoragePath, useRegistry: false);
        var failingHandler = new MockOfflineHttpMessageHandler();
        var httpClient = new HttpClient(failingHandler);

        var tempService = new LicenseService(storage, httpClient, enableTimers: false);
        string hwid = tempService.GetMachineHWID();
        byte[] machineKey = LicenseSecurity.DeriveMachineKey(hwid);

        // Issued 8 days ago (passed the 7-day hard limit)
        var issuedAt = DateTime.UtcNow.AddDays(-8);
        var hardLimit = issuedAt.AddDays(7);

        var lease = new OfflineLeasePayload
        {
            LicenseKey = "ADM-EXPIRED-OFFLINE-KEY",
            Username = "AdminUser",
            Type = LicenseType.Admin,
            HWID = hwid,
            LeaseIssuedAtUtc = issuedAt,
            OfflineGraceHardLimitUtc = hardLimit,
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1)
        };

        var record = new LicenseRecord
        {
            LicenseKey = lease.LicenseKey,
            Username = lease.Username,
            Type = lease.Type,
            Status = LicenseStatus.Active,
            HWID = hwid,
            LastValidatedAt = issuedAt,
            ExpiresAt = lease.ExpiresAtUtc,
            TrialStartDate = DateTime.UtcNow.AddDays(-30),
            EncryptedLease = LicenseSecurity.EncryptLease(lease, machineKey),
            LastKnownClockTicks = issuedAt.Ticks
        };

        storage.SaveLicenseRecord(record);

        // Recreate service so it loads the saved record
        var service = new LicenseService(storage, httpClient, enableTimers: false);
        var result = await service.ValidateCurrentLicenseAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.License);
        Assert.Equal(LicenseStatus.Expired, result.License.Status);
        Assert.Contains("Offline grace period expired", result.Message);
    }

    [Fact]
    public async Task LicenseService_ClockRollback_RevokesOfflineGraceImmediately()
    {
        var storage = new RegistryLicenseStorage(_tempStoragePath, useRegistry: false);

        // Store a high-water mark 2 hours in the future (simulating a rolled-back system clock)
        long futureTicks = DateTime.UtcNow.AddHours(2).Ticks;
        storage.UpdateLastKnownClockTicks(futureTicks);

        var failingHandler = new MockOfflineHttpMessageHandler();
        var httpClient = new HttpClient(failingHandler);

        var service = new LicenseService(storage, httpClient, enableTimers: false);
        var result = await service.ValidateCurrentLicenseAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.License);
        Assert.Equal(LicenseStatus.Invalid, result.License.Status);
        Assert.Contains("Clock manipulation detected", result.Message);
    }

    [Fact]
    public void RegistryLicenseStorage_RejectsTamperedFileStorage()
    {
        var storage = new RegistryLicenseStorage(_tempStoragePath, useRegistry: false);
        string hwid = "HWID-STORAGE-TEST-004";

        var record = new LicenseRecord
        {
            LicenseKey = "DEV-SAFE-KEY-111",
            Username = "SafeDev",
            Type = LicenseType.Dev,
            Status = LicenseStatus.Active,
            HWID = hwid,
            LastValidatedAt = DateTime.UtcNow,
            TrialStartDate = DateTime.UtcNow.AddDays(-5),
            LastKnownClockTicks = DateTime.UtcNow.Ticks
        };

        storage.SaveLicenseRecord(record);

        // Valid load
        var loaded = storage.LoadLicenseRecord();
        Assert.NotNull(loaded);
        Assert.Equal("DEV-SAFE-KEY-111", loaded.LicenseKey);

        // Malicious user modifies JSON file directly
        if (File.Exists(_tempStoragePath))
        {
            string json = File.ReadAllText(_tempStoragePath);
            json = json.Replace("SafeDev", "CrackedUser");
            File.WriteAllText(_tempStoragePath, json);

            // Reload should fail HMAC verification and return null!
            var tampered = storage.LoadLicenseRecord();
            Assert.Null(tampered);
        }
    }

    private class MockOfflineHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Simulated offline: Network interface disconnected.");
        }
    }
}
