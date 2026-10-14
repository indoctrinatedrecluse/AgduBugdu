using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Updates;

public class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;
}

public class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; set; } = new();
}

public class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? AssetFileName { get; set; }
    public long AssetSize { get; set; }
    public string? ChecksumsUrl { get; set; }
}

public record UpdateDownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    double ProgressPercentage,
    double BytesPerSecond
)
{
    public string FormattedProgress => TotalBytes > 0
        ? $"{BytesDownloaded / 1048576.0:F1} MB / {TotalBytes / 1048576.0:F1} MB ({ProgressPercentage:F0}%)"
        : $"{BytesDownloaded / 1048576.0:F1} MB";

    public string FormattedSpeed => BytesPerSecond > 0
        ? $"{BytesPerSecond / 1048576.0:F1} MB/s"
        : string.Empty;
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadUpdateAsync(
        UpdateCheckResult updateInfo,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    bool LaunchInstallerAndExit(string installerPath, bool silent = false);

    Task<bool> VerifyChecksumAsync(string filePath, string checksumsUrl, CancellationToken cancellationToken = default);
}
