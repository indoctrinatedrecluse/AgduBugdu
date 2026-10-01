using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using AgduBugdu.Core;
using AgduBugdu.Core.Updates;

namespace AgduBugdu.Infrastructure.Updates;

public class GitHubUpdateService : IUpdateService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static GitHubUpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AgduBugdu-Editor", AppVersionInfo.CurrentVersion));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
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
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return result;
            }

            var latestTag = release.TagName.TrimStart('v', 'V');
            result.LatestVersion = latestTag;
            result.ReleaseNotes = release.Body ?? string.Empty;
            result.ReleaseUrl = release.HtmlUrl ?? result.ReleaseUrl;

            if (Version.TryParse(currentVerStr, out var currentVer) &&
                Version.TryParse(latestTag, out var latestVer))
            {
                result.IsUpdateAvailable = latestVer > currentVer;
            }
        }
        catch (Exception)
        {
            // Silently fail update check if offline or network timeout
        }

        return result;
    }
}
