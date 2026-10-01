using System;
using System.Reflection;

namespace AgduBugdu.Core;

public static class AppVersionInfo
{
    private static readonly Lazy<string> _versionLazy = new(() =>
    {
        var asm = typeof(AppVersionInfo).Assembly;
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVersion))
        {
            // Strip git hash suffix if present (e.g. 1.0.0+hash)
            var plusIdx = infoVersion.IndexOf('+');
            return plusIdx > 0 ? infoVersion.Substring(0, plusIdx) : infoVersion;
        }

        var ver = asm.GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    });

    public static string CurrentVersion => _versionLazy.Value;
    public const string AppName = "AgduBugdu";
    public const string RepositoryOwner = "indoctrinatedrecluse";
    public const string RepositoryName = "AgduBugdu";
}
