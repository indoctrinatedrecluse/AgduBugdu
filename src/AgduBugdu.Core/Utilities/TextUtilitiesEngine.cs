using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgduBugdu.Core.Utilities;

public class JwtTokenInfo
{
    public bool IsValid { get; set; }
    public string HeaderJson { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public DateTime? Expiration { get; set; }
    public DateTime? IssuedAt { get; set; }
    public bool IsExpired => Expiration.HasValue && Expiration.Value < DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

public class HashBundle
{
    public string Md5 { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Sha512 { get; set; } = string.Empty;
}

public static class TextUtilitiesEngine
{
    public static string Base64Encode(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(bytes);
    }

    public static string Base64Decode(string base64Text)
    {
        if (string.IsNullOrWhiteSpace(base64Text)) return string.Empty;
        string cleaned = base64Text.Trim().Replace('-', '+').Replace('_', '/');
        switch (cleaned.Length % 4)
        {
            case 2: cleaned += "=="; break;
            case 3: cleaned += "="; break;
        }

        byte[] bytes = Convert.FromBase64String(cleaned);
        return Encoding.UTF8.GetString(bytes);
    }

    public static string UrlEncode(string plainText)
    {
        return string.IsNullOrEmpty(plainText) ? string.Empty : Uri.EscapeDataString(plainText);
    }

    public static string UrlDecode(string urlEncodedText)
    {
        return string.IsNullOrEmpty(urlEncodedText) ? string.Empty : Uri.UnescapeDataString(urlEncodedText);
    }

    public static JwtTokenInfo DecodeJwt(string token)
    {
        var info = new JwtTokenInfo();
        if (string.IsNullOrWhiteSpace(token))
        {
            info.IsValid = false;
            info.ErrorMessage = "Token is empty.";
            return info;
        }

        var parts = token.Trim().Split('.');
        if (parts.Length < 2)
        {
            info.IsValid = false;
            info.ErrorMessage = "Invalid JWT structure. Expected at least 2 dot-separated segments.";
            return info;
        }

        try
        {
            string headerRaw = Base64Decode(parts[0]);
            string payloadRaw = Base64Decode(parts[1]);

            using var headerDoc = JsonDocument.Parse(headerRaw);
            info.HeaderJson = JsonSerializer.Serialize(headerDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            if (headerDoc.RootElement.TryGetProperty("alg", out var algProp))
            {
                info.Algorithm = algProp.GetString() ?? string.Empty;
            }

            using var payloadDoc = JsonDocument.Parse(payloadRaw);
            info.PayloadJson = JsonSerializer.Serialize(payloadDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });

            if (payloadDoc.RootElement.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out long expSecs))
            {
                info.Expiration = DateTimeOffset.FromUnixTimeSeconds(expSecs).UtcDateTime;
            }

            if (payloadDoc.RootElement.TryGetProperty("iat", out var iatProp) && iatProp.TryGetInt64(out long iatSecs))
            {
                info.IssuedAt = DateTimeOffset.FromUnixTimeSeconds(iatSecs).UtcDateTime;
            }

            if (parts.Length >= 3)
            {
                info.Signature = parts[2];
            }

            info.IsValid = true;
        }
        catch (Exception ex)
        {
            info.IsValid = false;
            info.ErrorMessage = $"Failed to parse JWT: {ex.Message}";
        }

        return info;
    }

    public static HashBundle ComputeHashes(string input)
    {
        var bundle = new HashBundle();
        if (input == null) input = string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(input);

        using (var md5 = MD5.Create())
        {
            bundle.Md5 = Convert.ToHexString(md5.ComputeHash(bytes)).ToLowerInvariant();
        }

        using (var sha1 = SHA1.Create())
        {
            bundle.Sha1 = Convert.ToHexString(sha1.ComputeHash(bytes)).ToLowerInvariant();
        }

        using (var sha256 = SHA256.Create())
        {
            bundle.Sha256 = Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
        }

        using (var sha512 = SHA512.Create())
        {
            bundle.Sha512 = Convert.ToHexString(sha512.ComputeHash(bytes)).ToLowerInvariant();
        }

        return bundle;
    }

    public static string FormatJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string MinifyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
    }
}
