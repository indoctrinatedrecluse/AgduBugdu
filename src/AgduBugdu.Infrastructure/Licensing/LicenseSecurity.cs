using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgduBugdu.Core.Licensing;

namespace AgduBugdu.Infrastructure.Licensing;

public static class LicenseSecurity
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const byte PayloadFormatVersion = 0x01;

    private static readonly byte[] StaticSalt = SHA256.HashData(
        Encoding.UTF8.GetBytes("AgduBugdu::Licensing::HardwareKeyDerivation::v1.3.2::Salt"));

    /// <summary>
    /// Derives a cryptographically strong 256-bit machine key strictly bound to local hardware characteristics.
    /// A lease or registry entry copied to another machine will fail HMAC verification and AES-GCM decryption.
    /// </summary>
    public static byte[] DeriveMachineKey(string hwid)
    {
        string machineName = Environment.MachineName;
        string os = RuntimeInformation.OSDescription;
        string arch = RuntimeInformation.ProcessArchitecture.ToString();
        string machineGuid = string.Empty;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                machineGuid = key?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
            }
            catch { }
        }

        string rawEntropy = $"HWID:{hwid}#GUID:{machineGuid}#HOST:{machineName}#ARCH:{arch}#OS:{os}#PROD:agdubugdu";
        byte[] entropyBytes = Encoding.UTF8.GetBytes(rawEntropy);

        return Rfc2898DeriveBytes.Pbkdf2(
            entropyBytes,
            StaticSalt,
            iterations: 50_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
    }

    /// <summary>
    /// Encrypts an offline lease payload using authenticated AES-256-GCM.
    /// </summary>
    public static string EncryptLease(OfflineLeasePayload payload, byte[] machineKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(machineKey);

        payload.Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string json = JsonSerializer.Serialize(payload);
        byte[] plaintext = Encoding.UTF8.GetBytes(json);

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] tag = new byte[TagSizeBytes];
        byte[] ciphertext = new byte[plaintext.Length];

        using (var aesGcm = new AesGcm(machineKey, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Layout: [1 byte Version] + [12 bytes Nonce] + [16 bytes Tag] + [N bytes Ciphertext]
        byte[] result = new byte[1 + NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        result[0] = PayloadFormatVersion;
        Buffer.BlockCopy(nonce, 0, result, 1, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, 1 + NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, 1 + NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts and authenticates an offline lease payload. Throws CryptographicException if tampered or machine mismatch.
    /// </summary>
    public static OfflineLeasePayload DecryptLease(string encryptedBase64, byte[] machineKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedBase64);
        ArgumentNullException.ThrowIfNull(machineKey);

        byte[] raw = Convert.FromBase64String(encryptedBase64);
        const int minLength = 1 + NonceSizeBytes + TagSizeBytes;
        if (raw.Length < minLength || raw[0] != PayloadFormatVersion)
        {
            throw new CryptographicException("Invalid lease format or unsupported version.");
        }

        byte[] nonce = new byte[NonceSizeBytes];
        byte[] tag = new byte[TagSizeBytes];
        int cipherLength = raw.Length - minLength;
        byte[] ciphertext = new byte[cipherLength];
        byte[] plaintext = new byte[cipherLength];

        Buffer.BlockCopy(raw, 1, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(raw, 1 + NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(raw, minLength, ciphertext, 0, cipherLength);

        using (var aesGcm = new AesGcm(machineKey, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        string json = Encoding.UTF8.GetString(plaintext);
        var payload = JsonSerializer.Deserialize<OfflineLeasePayload>(json);
        if (payload == null)
        {
            throw new CryptographicException("Failed to deserialize decrypted offline lease payload.");
        }

        return payload;
    }

    /// <summary>
    /// Computes HMAC-SHA256 signature across all license record fields using the machine key.
    /// </summary>
    public static string ComputeIntegrityHmac(LicenseRecord record, byte[] machineKey)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(machineKey);

        string data = string.Join("|",
            record.LicenseKey ?? string.Empty,
            record.Username ?? string.Empty,
            record.Type.ToString(),
            record.Status.ToString(),
            record.HWID ?? string.Empty,
            FormatDateForHmac(record.ExpiresAt),
            FormatDateForHmac(record.TrialStartDate),
            FormatDateForHmac(record.LastValidatedAt),
            record.EncryptedLease ?? "NONE"
        );

        using var hmac = new HMACSHA256(machineKey);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private static string FormatDateForHmac(DateTime? dt)
    {
        if (!dt.HasValue) return "NULL";
        return dt.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    /// <summary>
    /// Verifies the integrity of a license record against tampering in constant time.
    /// </summary>
    public static bool VerifyIntegrityHmac(LicenseRecord record, byte[] machineKey)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.IntegrityHmac))
            return false;

        try
        {
            string expectedHmac = ComputeIntegrityHmac(record, machineKey);
            byte[] expectedBytes = Convert.FromBase64String(expectedHmac);
            byte[] actualBytes = Convert.FromBase64String(record.IntegrityHmac);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if system clock has rolled backwards past the acceptable skew window (5 minutes).
    /// </summary>
    public static bool IsClockRollbackDetected(long currentUtcTicks, long lastKnownUtcTicks, out TimeSpan rollbackDuration)
    {
        long skewThresholdTicks = TimeSpan.FromMinutes(5).Ticks;

        if (currentUtcTicks < (lastKnownUtcTicks - skewThresholdTicks))
        {
            rollbackDuration = TimeSpan.FromTicks(lastKnownUtcTicks - currentUtcTicks);
            return true;
        }

        rollbackDuration = TimeSpan.Zero;
        return false;
    }
}
