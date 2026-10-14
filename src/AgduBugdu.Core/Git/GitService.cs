using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgduBugdu.Core.Git;

public interface IGitService
{
    Task<bool> IsGitInstalledAsync();
    Task<GitStatusResult> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken = default);
    Task<bool> StageFileAsync(string workspaceRoot, string relativePath);
    Task<bool> StageAllAsync(string workspaceRoot);
    Task<bool> UnstageFileAsync(string workspaceRoot, string relativePath);
    Task<bool> UnstageAllAsync(string workspaceRoot);
    Task<bool> DiscardChangesAsync(string workspaceRoot, string relativePath);
    Task<GitCommitResult> CommitAsync(string workspaceRoot, string message);
    Task<string> GetDiffAsync(string workspaceRoot, string? relativePath = null, bool staged = false);
    Task<IReadOnlyList<GitLogEntry>> GetRecentCommitsAsync(string workspaceRoot, int maxCount = 10);
}

public class GitService : IGitService
{
    private static readonly Regex BranchHeaderRegex = new(
        @"^##\s+(?:(?:No commits yet on )?(?<branch>[^\s\.]+)|HEAD \(no branch\))(?:\.\.\.(?<upstream>[^\s]+))?(?:\s+\[(?:ahead\s+(?<ahead>\d+))?(?:,?\s*behind\s+(?<behind>\d+))?\])?",
        RegexOptions.Compiled);

    public async Task<bool> IsGitInstalledAsync()
    {
        try
        {
            var (exitCode, _) = await RunGitCommandAsync(null, "--version");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<GitStatusResult> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return GitStatusResult.NotARepository();

        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, "status --porcelain=v1 -b -uall", cancellationToken);
        if (exitCode != 0)
        {
            return GitStatusResult.NotARepository();
        }

        return ParseGitStatus(workspaceRoot, stdout);
    }

    public async Task<bool> StageFileAsync(string workspaceRoot, string relativePath)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"add -- \"{EscapeArg(relativePath)}\"");
        return exitCode == 0;
    }

    public async Task<bool> StageAllAsync(string workspaceRoot)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, "add -A");
        return exitCode == 0;
    }

    public async Task<bool> UnstageFileAsync(string workspaceRoot, string relativePath)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"restore --staged -- \"{EscapeArg(relativePath)}\"");
        if (exitCode != 0)
        {
            (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"reset HEAD -- \"{EscapeArg(relativePath)}\"");
        }
        return exitCode == 0;
    }

    public async Task<bool> UnstageAllAsync(string workspaceRoot)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, "restore --staged .");
        if (exitCode != 0)
        {
            (exitCode, _) = await RunGitCommandAsync(workspaceRoot, "reset HEAD");
        }
        return exitCode == 0;
    }

    public async Task<bool> DiscardChangesAsync(string workspaceRoot, string relativePath)
    {
        string fullPath = Path.Combine(workspaceRoot, relativePath);

        // Try git restore for tracked files
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"restore -- \"{EscapeArg(relativePath)}\"");
        if (exitCode != 0)
        {
            (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"checkout -- \"{EscapeArg(relativePath)}\"");
        }

        // If it was untracked, clean it
        if (exitCode != 0 && File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return exitCode == 0;
    }

    public async Task<GitCommitResult> CommitAsync(string workspaceRoot, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new GitCommitResult(false, null, string.Empty, "Commit message cannot be empty.");

        string sanitized = message.Replace("\"", "\\\"");
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, $"commit -m \"{sanitized}\"");

        if (exitCode == 0)
        {
            // Extract commit hash if available
            string hash = string.Empty;
            var match = Regex.Match(stdout, @"\[(?:\S+\s+)?([a-f0-9]{7,})\]");
            if (match.Success)
            {
                hash = match.Groups[1].Value;
            }
            return new GitCommitResult(true, hash, message);
        }

        return new GitCommitResult(false, null, message, stdout);
    }

    public async Task<string> GetDiffAsync(string workspaceRoot, string? relativePath = null, bool staged = false)
    {
        string cmd = "diff";
        if (staged) cmd += " --cached";
        if (!string.IsNullOrWhiteSpace(relativePath)) cmd += $" -- \"{EscapeArg(relativePath)}\"";

        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, cmd);
        return exitCode == 0 ? stdout : string.Empty;
    }

    public async Task<IReadOnlyList<GitLogEntry>> GetRecentCommitsAsync(string workspaceRoot, int maxCount = 10)
    {
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, $"log -n {maxCount} --format=\"%H|%h|%an|%ad|%s\" --date=short");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<GitLogEntry>();

        var entries = new List<GitLogEntry>();
        using var reader = new StringReader(stdout);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split('|');
            if (parts.Length >= 5)
            {
                entries.Add(new GitLogEntry(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts[3].Trim(),
                    string.Join('|', parts.Skip(4)).Trim()
                ));
            }
        }
        return entries;
    }

    public static GitStatusResult ParseGitStatus(string workspaceRoot, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return new GitStatusResult(true, "main", 0, 0, Array.Empty<GitFileItem>(), Array.Empty<GitFileItem>(), Array.Empty<GitFileItem>());

        string branch = "HEAD";
        int ahead = 0;
        int behind = 0;

        var staged = new List<GitFileItem>();
        var unstaged = new List<GitFileItem>();
        var untracked = new List<GitFileItem>();

        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("##"))
            {
                var match = BranchHeaderRegex.Match(line);
                if (match.Success)
                {
                    if (match.Groups["branch"].Success)
                        branch = match.Groups["branch"].Value;
                    if (match.Groups["ahead"].Success && int.TryParse(match.Groups["ahead"].Value, out int a))
                        ahead = a;
                    if (match.Groups["behind"].Success && int.TryParse(match.Groups["behind"].Value, out int b))
                        behind = b;
                }
                continue;
            }

            if (line.Length < 3)
                continue;

            char stagedCode = line[0];
            char worktreeCode = line[1];
            string pathPart = line[3..].Trim();

            // Handle rename syntax: "orig.txt -> new.txt"
            if (pathPart.Contains(" -> "))
            {
                var renameParts = pathPart.Split(" -> ");
                pathPart = renameParts[1].Trim().Trim('"');
            }
            else
            {
                pathPart = pathPart.Trim('"');
            }

            string fullPath = Path.Combine(workspaceRoot, pathPart);

            // Untracked files
            if (stagedCode == '?' && worktreeCode == '?')
            {
                untracked.Add(new GitFileItem(fullPath, pathPart, GitFileStatusType.Untracked, false));
                continue;
            }

            // Staged changes (X)
            if (stagedCode != ' ' && stagedCode != '?')
            {
                var status = MapStatus(stagedCode);
                staged.Add(new GitFileItem(fullPath, pathPart, status, true));
            }

            // Unstaged changes (Y)
            if (worktreeCode != ' ' && worktreeCode != '?')
            {
                var status = MapStatus(worktreeCode);
                unstaged.Add(new GitFileItem(fullPath, pathPart, status, false));
            }
        }

        return new GitStatusResult(true, branch, ahead, behind, staged, unstaged, untracked);
    }

    private static GitFileStatusType MapStatus(char code) => code switch
    {
        'M' => GitFileStatusType.Modified,
        'A' => GitFileStatusType.Added,
        'D' => GitFileStatusType.Deleted,
        'R' => GitFileStatusType.Renamed,
        'C' => GitFileStatusType.Copied,
        'U' => GitFileStatusType.Conflict,
        '?' => GitFileStatusType.Untracked,
        _ => GitFileStatusType.Unknown
    };

    private static string EscapeArg(string arg) => arg.Replace("\"", "\\\"");

    private static async Task<(int ExitCode, string Output)> RunGitCommandAsync(
        string? workingDir,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
        {
            psi.WorkingDirectory = workingDir;
        }

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            string combined = !string.IsNullOrWhiteSpace(stdout) ? stdout : stderr;
            return (process.ExitCode, combined);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
