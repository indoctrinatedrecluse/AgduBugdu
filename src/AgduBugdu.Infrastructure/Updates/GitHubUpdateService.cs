using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.Core;
using AgduBugdu.Core.Updates;

namespace AgduBugdu.Infrastructure.Updates;

public class GitHubUpdateService : IUpdateService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static GitHubUpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AgduBugdu-Editor", AppVersionInfo.CurrentVersion));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVerStr = AppVersionInfo.CurrentVersion;
        var result = new UpdateCheckResult
        {
            CurrentVersion = currentVerStr,
            LatestVersion = currentVerStr,
            IsUpdateAvailable = false,
            ReleaseNotes = string.Empty,
            ReleaseUrl = $"https://github.com/{AppVersionInfo.RepositoryOwner}/{AppVersionInfo.RepositoryName}/releases"
        };

        try
        {
            var url = $"https://api.github.com/repos/{AppVersionInfo.RepositoryOwner}/{AppVersionInfo.RepositoryName}/releases/latest";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return result;
            }

            var latestTag = release.TagName.TrimStart('v', 'V');
            result.LatestVersion = latestTag;
            result.ReleaseNotes = release.Body ?? string.Empty;
            result.ReleaseUrl = release.HtmlUrl ?? result.ReleaseUrl;

            result.IsUpdateAvailable = IsNewerVersion(currentVerStr, latestTag);

            if (release.Assets != null && release.Assets.Count > 0)
            {
                var targetAsset = SelectTargetAsset(release.Assets);
                if (targetAsset != null)
                {
                    result.DownloadUrl = targetAsset.BrowserDownloadUrl;
                    result.AssetFileName = targetAsset.Name;
                    result.AssetSize = targetAsset.Size;
                }

                var checksumAsset = release.Assets.FirstOrDefault(a =>
                    string.Equals(a.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
                if (checksumAsset != null)
                {
                    result.ChecksumsUrl = checksumAsset.BrowserDownloadUrl;
                }
            }
        }
        catch
        {
            // Silently fail update check if offline or network timeout
        }

        return result;
    }

    public async Task<string> DownloadUpdateAsync(
        UpdateCheckResult updateInfo,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            throw new InvalidOperationException("No download URL available for this update.");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "AgduBugdu_Updates");
        Directory.CreateDirectory(tempDir);

        string fileName = !string.IsNullOrWhiteSpace(updateInfo.AssetFileName)
            ? updateInfo.AssetFileName
            : $"AgduBugdu-v{updateInfo.LatestVersion}-installer.exe";

        string destinationPath = Path.Combine(tempDir, fileName);

        using var response = await _httpClient.GetAsync(
            updateInfo.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? updateInfo.AssetSize;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        var stopwatch = Stopwatch.StartNew();

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double bytesPerSecond = elapsedSeconds > 0 ? totalRead / elapsedSeconds : 0;
            double percentage = totalBytes > 0 ? Math.Min(100.0, (double)totalRead / totalBytes * 100.0) : 0;

            progress?.Report(new UpdateDownloadProgress(totalRead, totalBytes, percentage, bytesPerSecond));
        }

        await fileStream.FlushAsync(cancellationToken);

        return destinationPath;
    }

    public bool LaunchInstallerAndExit(string installerPath, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            return false;
        }

        try
        {
            if (installerPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                if (silent)
                {
                    psi.Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";
                }

                var process = Process.Start(psi);
                if (process != null)
                {
                    // Terminate current application so installer can overwrite and relaunch
                    Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        Environment.Exit(0);
                    });
                    return true;
                }
            }
            else
            {
                // Non-exe packages (e.g. portable zip or tar.gz)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{installerPath}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.GetDirectoryName(installerPath) ?? installerPath,
                        UseShellExecute = true
                    });
                }
                return true;
            }
        }
        catch
        {
            // Return false if launch failed (e.g. UAC prompt declined)
        }

        return false;
    }

    public async Task<bool> VerifyChecksumAsync(string filePath, string checksumsUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrWhiteSpace(checksumsUrl))
            return false;

        try
        {
            var response = await _httpClient.GetAsync(checksumsUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var checksumContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var fileName = Path.GetFileName(filePath);

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            byte[] hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            string computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var lines = checksumContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var expectedHash = parts[0].Trim().ToLowerInvariant();
                        return string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(latestVersion))
            return false;

        string cleanCurrent = currentVersion.TrimStart('v', 'V').Split('+')[0].Split('-')[0];
        string cleanLatest = latestVersion.TrimStart('v', 'V').Split('+')[0].Split('-')[0];

        if (Version.TryParse(cleanCurrent, out var cur) && Version.TryParse(cleanLatest, out var lat))
        {
            return lat > cur;
        }

        return string.Compare(cleanLatest, cleanCurrent, StringComparison.OrdinalIgnoreCase) > 0;
    }

    public static GitHubReleaseAsset? SelectTargetAsset(
        IEnumerable<GitHubReleaseAsset> assets,
        OSPlatform? targetOs = null,
        Architecture? targetArch = null)
    {
        var list = assets.ToList();
        if (list.Count == 0) return null;

        bool isWindows = targetOs.HasValue
            ? targetOs.Value == OSPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isLinux = targetOs.HasValue
            ? targetOs.Value == OSPlatform.Linux
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        bool isOsx = targetOs.HasValue
            ? targetOs.Value == OSPlatform.OSX
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        bool isArm64 = targetArch.HasValue
            ? targetArch.Value == Architecture.Arm64
            : RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        if (isWindows)
        {
            // Prefer installer setup exe
            var setupExe = list.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase));
            if (setupExe != null) return setupExe;

            // Fallback to any exe
            var anyExe = list.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (anyExe != null) return anyExe;

            // Fallback to win zip
            return list.FirstOrDefault(a => a.Name.Contains("win", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }

        if (isLinux)
        {
            var linuxTar = list.FirstOrDefault(a => a.Name.Contains("linux", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
            if (linuxTar != null) return linuxTar;

            return list.FirstOrDefault(a => a.Name.Contains("linux", StringComparison.OrdinalIgnoreCase));
        }

        if (isOsx)
        {
            if (isArm64)
            {
                var armTar = list.FirstOrDefault(a => a.Name.Contains("osx-arm64", StringComparison.OrdinalIgnoreCase));
                if (armTar != null) return armTar;
            }

            var osxTar = list.FirstOrDefault(a => a.Name.Contains("osx", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
            if (osxTar != null) return osxTar;

            return list.FirstOrDefault(a => a.Name.Contains("osx", StringComparison.OrdinalIgnoreCase));
        }

        return list.FirstOrDefault();
    }
}
