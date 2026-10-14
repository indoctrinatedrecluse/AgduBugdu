using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Docker;

public class DockerCliService
{
    private bool? _isDockerAvailable;
    private readonly List<DockerContainerItem> _mockContainers = new();
    private readonly List<DockerImageItem> _mockImages = new();

    public bool IsDemoMode { get; set; }

    public DockerCliService()
    {
        InitializeMockData();
    }

    private void InitializeMockData()
    {
        _mockContainers.Clear();
        _mockContainers.Add(new DockerContainerItem
        {
            Id = "c8f93e2b10a4",
            Name = "agdubugdu-api",
            Image = "agdubugdu/backend:v1.5.0",
            State = "running",
            Status = "Up 3 hours",
            Ports = "0.0.0.0:5000->5000/tcp",
            Created = "3 hours ago"
        });
        _mockContainers.Add(new DockerContainerItem
        {
            Id = "d7a421b9e8c3",
            Name = "postgres-db",
            Image = "postgres:16-alpine",
            State = "running",
            Status = "Up 3 hours",
            Ports = "0.0.0.0:5432->5432/tcp",
            Created = "3 hours ago"
        });
        _mockContainers.Add(new DockerContainerItem
        {
            Id = "f12c849da03e",
            Name = "redis-cache",
            Image = "redis:7.2-alpine",
            State = "running",
            Status = "Up 2 hours",
            Ports = "0.0.0.0:6379->6379/tcp",
            Created = "2 hours ago"
        });
        _mockContainers.Add(new DockerContainerItem
        {
            Id = "9e4a11b0c72d",
            Name = "nginx-proxy",
            Image = "nginx:alpine",
            State = "exited",
            Status = "Exited (0) 45 minutes ago",
            Ports = "0.0.0.0:80->80/tcp",
            Created = "1 day ago"
        });

        _mockImages.Clear();
        _mockImages.Add(new DockerImageItem
        {
            Id = "sha256:7b91c0a823e1",
            Repository = "agdubugdu/backend",
            Tag = "v1.5.0",
            Size = "215 MB",
            Created = "3 days ago"
        });
        _mockImages.Add(new DockerImageItem
        {
            Id = "sha256:4a8e29d10c5f",
            Repository = "postgres",
            Tag = "16-alpine",
            Size = "379 MB",
            Created = "2 weeks ago"
        });
        _mockImages.Add(new DockerImageItem
        {
            Id = "sha256:3e9a41c28f0b",
            Repository = "redis",
            Tag = "7.2-alpine",
            Size = "42 MB",
            Created = "1 month ago"
        });
    }

    public async Task<bool> CheckDockerAvailableAsync()
    {
        if (IsDemoMode)
        {
            _isDockerAvailable = false;
            return false;
        }

        if (_isDockerAvailable.HasValue)
            return _isDockerAvailable.Value;

        try
        {
            var res = await RunProcessAsync("docker", "info", 3000);
            _isDockerAvailable = res.ExitCode == 0;
            IsDemoMode = !_isDockerAvailable.Value;
        }
        catch
        {
            _isDockerAvailable = false;
            IsDemoMode = true;
        }

        return _isDockerAvailable.Value;
    }

    public async Task<List<DockerContainerItem>> GetContainersAsync(bool all = true)
    {
        bool available = await CheckDockerAvailableAsync();
        if (!available)
        {
            return _mockContainers.ToList();
        }

        string args = all
            ? "ps -a --format \"{{.ID}}\t{{.Names}}\t{{.Image}}\t{{.State}}\t{{.Status}}\t{{.Ports}}\t{{.CreatedAt}}\""
            : "ps --format \"{{.ID}}\t{{.Names}}\t{{.Image}}\t{{.State}}\t{{.Status}}\t{{.Ports}}\t{{.CreatedAt}}\"";

        var res = await RunProcessAsync("docker", args, 5000);
        if (res.ExitCode != 0 || string.IsNullOrWhiteSpace(res.Output))
        {
            return _mockContainers.ToList();
        }

        return ParseContainerLines(res.Output);
    }

    public static List<DockerContainerItem> ParseContainerLines(string output)
    {
        var items = new List<DockerContainerItem>();
        if (string.IsNullOrWhiteSpace(output))
            return items;

        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length >= 4)
            {
                items.Add(new DockerContainerItem
                {
                    Id = parts[0].Trim(),
                    Name = parts[1].Trim(),
                    Image = parts[2].Trim(),
                    State = parts[3].Trim().ToLowerInvariant(),
                    Status = parts.Length > 4 ? parts[4].Trim() : string.Empty,
                    Ports = parts.Length > 5 ? parts[5].Trim() : string.Empty,
                    Created = parts.Length > 6 ? parts[6].Trim() : string.Empty
                });
            }
        }

        return items;
    }

    public async Task<List<DockerImageItem>> GetImagesAsync()
    {
        bool available = await CheckDockerAvailableAsync();
        if (!available)
        {
            return _mockImages.ToList();
        }

        string args = "images --format \"{{.ID}}\t{{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}\"";
        var res = await RunProcessAsync("docker", args, 5000);
        if (res.ExitCode != 0 || string.IsNullOrWhiteSpace(res.Output))
        {
            return _mockImages.ToList();
        }

        return ParseImageLines(res.Output);
    }

    public static List<DockerImageItem> ParseImageLines(string output)
    {
        var items = new List<DockerImageItem>();
        if (string.IsNullOrWhiteSpace(output))
            return items;

        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length >= 4)
            {
                items.Add(new DockerImageItem
                {
                    Id = parts[0].Trim(),
                    Repository = parts[1].Trim(),
                    Tag = parts[2].Trim(),
                    Size = parts[3].Trim(),
                    Created = parts.Length > 4 ? parts[4].Trim() : string.Empty
                });
            }
        }

        return items;
    }

    public async Task<string> StartContainerAsync(string id)
    {
        if (IsDemoMode)
        {
            var match = _mockContainers.FirstOrDefault(c => c.Id == id || c.Name == id);
            if (match != null)
            {
                match.State = "running";
                match.Status = "Up Just now";
            }
            return $"Container {id} started (demo).";
        }

        var res = await RunProcessAsync("docker", $"start {id}", 10000);
        return res.Output;
    }

    public async Task<string> StopContainerAsync(string id)
    {
        if (IsDemoMode)
        {
            var match = _mockContainers.FirstOrDefault(c => c.Id == id || c.Name == id);
            if (match != null)
            {
                match.State = "exited";
                match.Status = "Exited (0) Just now";
            }
            return $"Container {id} stopped (demo).";
        }

        var res = await RunProcessAsync("docker", $"stop {id}", 15000);
        return res.Output;
    }

    public async Task<string> RestartContainerAsync(string id)
    {
        if (IsDemoMode)
        {
            var match = _mockContainers.FirstOrDefault(c => c.Id == id || c.Name == id);
            if (match != null)
            {
                match.State = "running";
                match.Status = "Up Just now (Restarted)";
            }
            return $"Container {id} restarted (demo).";
        }

        var res = await RunProcessAsync("docker", $"restart {id}", 15000);
        return res.Output;
    }

    public async Task<string> PauseContainerAsync(string id)
    {
        if (IsDemoMode)
        {
            var match = _mockContainers.FirstOrDefault(c => c.Id == id || c.Name == id);
            if (match != null)
            {
                if (match.State == "paused")
                {
                    match.State = "running";
                    match.Status = "Up (Unpaused)";
                }
                else
                {
                    match.State = "paused";
                    match.Status = "Paused";
                }
            }
            return $"Toggled pause on {id} (demo).";
        }

        var res = await RunProcessAsync("docker", $"pause {id}", 10000);
        return res.Output;
    }

    public async Task<string> RemoveContainerAsync(string id, bool force = true)
    {
        if (IsDemoMode)
        {
            _mockContainers.RemoveAll(c => c.Id == id || c.Name == id);
            return $"Container {id} removed (demo).";
        }

        string forceFlag = force ? "-f" : "";
        var res = await RunProcessAsync("docker", $"rm {forceFlag} {id}", 10000);
        return res.Output;
    }

    public async Task<string> GetContainerLogsAsync(string id, int tailLines = 150)
    {
        if (IsDemoMode)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Demo Logs for container {id}]");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] System process initialized.");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Listening on 0.0.0.0:5000 (TCP)...");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Ready for incoming connections.");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] GET /health -> 200 OK (0.4ms)");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Worker #1 spawned and active.");
            return sb.ToString();
        }

        var res = await RunProcessAsync("docker", $"logs --tail {tailLines} {id}", 10000);
        return string.IsNullOrWhiteSpace(res.Output) && !string.IsNullOrWhiteSpace(res.Error)
            ? res.Error
            : res.Output;
    }

    public async Task<string> RemoveImageAsync(string id)
    {
        if (IsDemoMode)
        {
            _mockImages.RemoveAll(i => i.Id == id || i.Repository == id);
            return $"Image {id} deleted (demo).";
        }

        var res = await RunProcessAsync("docker", $"rmi -f {id}", 10000);
        return res.Output;
    }

    public async Task<string> PruneUnusedContainersAsync()
    {
        if (IsDemoMode)
        {
            _mockContainers.RemoveAll(c => c.State == "exited");
            return "Pruned all stopped containers (demo).";
        }

        var res = await RunProcessAsync("docker", "container prune -f", 10000);
        return res.Output;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string filename, string args, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = filename,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(timeoutMs);
            await process.WaitForExitAsync(cts.Token);

            return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            return (-1, outputBuilder.ToString(), "Process timed out.");
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }
}
