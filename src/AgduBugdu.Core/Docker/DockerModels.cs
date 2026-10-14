using System;

namespace AgduBugdu.Core.Docker;

public class DockerContainerItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string State { get; set; } = "created";
    public string Status { get; set; } = string.Empty;
    public string Ports { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;

    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);
    public bool IsPaused => State.Equals("paused", StringComparison.OrdinalIgnoreCase);

    public string ShortId => Id.Length > 12 ? Id.Substring(0, 12) : Id;

    public string StateBadgeColor => State.ToLowerInvariant() switch
    {
        "running" => "#10B981",    // Emerald
        "exited" => "#EF4444",     // Red
        "paused" => "#F59E0B",     // Amber
        "restarting" => "#3B82F6", // Blue
        _ => "#888888"
    };

    public string StateIcon => State.ToLowerInvariant() switch
    {
        "running" => "▶",
        "exited" => "⏹",
        "paused" => "⏸",
        "restarting" => "🔄",
        _ => "•"
    };
}

public class DockerImageItem
{
    public string Id { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Tag { get; set; } = "latest";
    public string Size { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;

    public string FullName => $"{Repository}:{Tag}";
    public string ShortId => Id.Length > 12 ? Id.Substring(0, 12) : Id;
}
