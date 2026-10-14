using System;

namespace AgduBugdu.Core.Licensing;

public enum LicenseType
{
    Trial,
    User,
    Dev,
    Admin
}

public enum LicenseStatus
{
    TrialActive,
    TrialExpired,
    Active,
    Expired,
    Suspended,
    Revoked,
    Unregistered,
    Invalid
}

public class LicenseRecord
{
    public string LicenseKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public LicenseType Type { get; set; } = LicenseType.Trial;
    public LicenseStatus Status { get; set; } = LicenseStatus.TrialActive;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? TrialStartDate { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public string HWID { get; set; } = string.Empty;
}

public class LicenseInfo
{
    public string LicenseKey { get; set; } = string.Empty;
    public LicenseType Type { get; set; } = LicenseType.Trial;
    public LicenseStatus Status { get; set; } = LicenseStatus.TrialActive;
    public string Username { get; set; } = string.Empty;
    public string HWID { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? TrialStartDate { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public int? GraceDaysLeft { get; set; }

    public bool IsPaid => Type == LicenseType.User || Type == LicenseType.Dev || Type == LicenseType.Admin;
    public bool IsValid => Status == LicenseStatus.Active || Status == LicenseStatus.TrialActive;

    public int DaysRemaining
    {
        get
        {
            if (Type == LicenseType.Trial)
            {
                var start = TrialStartDate ?? DateTime.UtcNow;
                var elapsed = (DateTime.UtcNow - start).TotalDays;
                return Math.Max(0, 60 - (int)elapsed);
            }

            if (ExpiresAt.HasValue)
            {
                var remaining = (ExpiresAt.Value - DateTime.UtcNow).TotalDays;
                return Math.Max(0, (int)remaining);
            }

            return int.MaxValue;
        }
    }

    public string DisplayTitle => Type switch
    {
        LicenseType.Admin => "Admin License",
        LicenseType.Dev => "Dev License",
        LicenseType.User => "User License",
        _ => "Trial License"
    };

    public string BadgeIcon => Type switch
    {
        LicenseType.Admin => "👑",
        LicenseType.Dev => "⚡",
        LicenseType.User => "👤",
        _ => "⏳"
    };

    public string FormattedValidity
    {
        get
        {
            if (Type == LicenseType.Trial)
            {
                return DaysRemaining > 0
                    ? $"{DaysRemaining} days remaining in trial"
                    : "Trial expired (60-day period ended)";
            }

            if (ExpiresAt.HasValue)
            {
                return $"Expires: {ExpiresAt.Value:yyyy-MM-dd} ({DaysRemaining} days remaining)";
            }

            return "Perpetual / Lifetime License";
        }
    }
}

public class LicenseValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public LicenseInfo? License { get; set; }
}
