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
    Task<GitCommitResult> CommitAsync(string workspaceRoot, string message, bool amend = false);
    Task<string> GetDiffAsync(string workspaceRoot, string? relativePath = null, bool staged = false);
    Task<IReadOnlyList<GitLogEntry>> GetRecentCommitsAsync(string workspaceRoot, int maxCount = 25);
    Task<IReadOnlyList<GitBranch>> GetBranchesAsync(string workspaceRoot);
    Task<bool> CreateBranchAsync(string workspaceRoot, string branchName, bool checkout = true);
    Task<bool> CheckoutBranchAsync(string workspaceRoot, string branchName);
    Task<bool> DeleteBranchAsync(string workspaceRoot, string branchName, bool force = false);
    Task<bool> MergeBranchAsync(string workspaceRoot, string branchName);
    Task<IReadOnlyList<GitTag>> GetTagsAsync(string workspaceRoot);
    Task<bool> CreateTagAsync(string workspaceRoot, string tagName, string? message = null);
    Task<bool> DeleteTagAsync(string workspaceRoot, string tagName);
    Task<IReadOnlyList<GitStash>> GetStashesAsync(string workspaceRoot);
    Task<bool> StashSaveAsync(string workspaceRoot, string? message = null);
    Task<bool> StashPopAsync(string workspaceRoot, int index = 0);
    Task<bool> StashApplyAsync(string workspaceRoot, int index = 0);
    Task<bool> StashDropAsync(string workspaceRoot, int index = 0);
    Task<(bool Success, string Message)> FetchAsync(string workspaceRoot);
    Task<(bool Success, string Message)> PullAsync(string workspaceRoot);
    Task<(bool Success, string Message)> PushAsync(string workspaceRoot);
    Task<GitCommitDetail?> GetCommitDetailAsync(string workspaceRoot, string commitHash);
    Task<string?> GetHeadFileContentAsync(string workspaceRoot, string relativePath);
    DiffFileModel ParseDiff(string rawDiff);
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

    public async Task<GitCommitResult> CommitAsync(string workspaceRoot, string message, bool amend = false)
    {
        if (string.IsNullOrWhiteSpace(message) && !amend)
            return new GitCommitResult(false, null, string.Empty, "Commit message cannot be empty.");

        string cmd = "commit";
        if (amend) cmd += " --amend";
        if (!string.IsNullOrWhiteSpace(message))
        {
            string sanitized = message.Replace("\"", "\\\"");
            cmd += $" -m \"{sanitized}\"";
        }

        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, cmd);

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

    public async Task<IReadOnlyList<GitLogEntry>> GetRecentCommitsAsync(string workspaceRoot, int maxCount = 25)
    {
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, $"log -n {maxCount} --format=format:%H%x7c%h%x7c%an%x7c%ad%x7c%s --date=short");
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

    public async Task<IReadOnlyList<GitBranch>> GetBranchesAsync(string workspaceRoot)
    {
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, "branch -a -v --no-color");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<GitBranch>();

        var branches = new List<GitBranch>();
        using var reader = new StringReader(stdout);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Filter out symbolic pointers like remotes/origin/HEAD -> origin/master
            if (trimmed.Contains("->")) continue;

            bool isCurrent = line.TrimStart().StartsWith("*");
            string branchData = trimmed.TrimStart('*').Trim();
            var tokens = branchData.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            string branchName = tokens[0];
            bool isRemote = branchName.StartsWith("remotes/");
            string? hash = tokens.Length > 1 ? tokens[1] : null;
            string? subject = tokens.Length > 2 ? string.Join(' ', tokens.Skip(2)) : null;

            branches.Add(new GitBranch(
                branchName,
                isCurrent,
                isRemote,
                null,
                hash,
                subject
            ));
        }

        return branches;
    }

    public async Task<bool> CreateBranchAsync(string workspaceRoot, string branchName, bool checkout = true)
    {
        string cmd = checkout ? $"checkout -b \"{EscapeArg(branchName)}\"" : $"branch \"{EscapeArg(branchName)}\"";
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, cmd);
        return exitCode == 0;
    }

    public async Task<bool> CheckoutBranchAsync(string workspaceRoot, string branchName)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"checkout \"{EscapeArg(branchName)}\"");
        return exitCode == 0;
    }

    public async Task<bool> DeleteBranchAsync(string workspaceRoot, string branchName, bool force = false)
    {
        string flag = force ? "-D" : "-d";
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"branch {flag} \"{EscapeArg(branchName)}\"");
        return exitCode == 0;
    }

    public async Task<bool> MergeBranchAsync(string workspaceRoot, string branchName)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"merge \"{EscapeArg(branchName)}\"");
        return exitCode == 0;
    }

    public async Task<IReadOnlyList<GitTag>> GetTagsAsync(string workspaceRoot)
    {
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, "tag -l -n1");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<GitTag>();

        var tags = new List<GitTag>();
        using var reader = new StringReader(stdout);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string tagName = parts[0];
            string? note = parts.Length > 1 ? parts[1].Trim() : null;
            tags.Add(new GitTag(tagName, null, note));
        }
        return tags;
    }

    public async Task<bool> CreateTagAsync(string workspaceRoot, string tagName, string? message = null)
    {
        string cmd = string.IsNullOrWhiteSpace(message)
            ? $"tag \"{EscapeArg(tagName)}\""
            : $"tag -a \"{EscapeArg(tagName)}\" -m \"{EscapeArg(message)}\"";

        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, cmd);
        return exitCode == 0;
    }

    public async Task<bool> DeleteTagAsync(string workspaceRoot, string tagName)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"tag -d \"{EscapeArg(tagName)}\"");
        return exitCode == 0;
    }

    public async Task<IReadOnlyList<GitStash>> GetStashesAsync(string workspaceRoot)
    {
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, "stash list");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<GitStash>();

        var stashes = new List<GitStash>();
        using var reader = new StringReader(stdout);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            // e.g. stash@{0}: WIP on master: 63d9728 feat...
            var match = Regex.Match(line, @"stash@\{(\d+)\}:\s*(.*)");
            if (match.Success)
            {
                int idx = int.Parse(match.Groups[1].Value);
                string msg = match.Groups[2].Value.Trim();
                stashes.Add(new GitStash(idx, $"stash@{{{idx}}}", msg, DateTime.Now.ToString("g")));
            }
        }
        return stashes;
    }

    public async Task<bool> StashSaveAsync(string workspaceRoot, string? message = null)
    {
        string cmd = string.IsNullOrWhiteSpace(message)
            ? "stash push"
            : $"stash push -m \"{EscapeArg(message)}\"";
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, cmd);
        return exitCode == 0;
    }

    public async Task<bool> StashPopAsync(string workspaceRoot, int index = 0)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"stash pop stash@{{{index}}}");
        return exitCode == 0;
    }

    public async Task<bool> StashApplyAsync(string workspaceRoot, int index = 0)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"stash apply stash@{{{index}}}");
        return exitCode == 0;
    }

    public async Task<bool> StashDropAsync(string workspaceRoot, int index = 0)
    {
        var (exitCode, _) = await RunGitCommandAsync(workspaceRoot, $"stash drop stash@{{{index}}}");
        return exitCode == 0;
    }

    public async Task<(bool Success, string Message)> FetchAsync(string workspaceRoot)
    {
        var (exitCode, output) = await RunGitCommandAsync(workspaceRoot, "fetch --all --prune");
        return (exitCode == 0, output.Trim());
    }

    public async Task<(bool Success, string Message)> PullAsync(string workspaceRoot)
    {
        var (exitCode, output) = await RunGitCommandAsync(workspaceRoot, "pull");
        return (exitCode == 0, output.Trim());
    }

    public async Task<(bool Success, string Message)> PushAsync(string workspaceRoot)
    {
        var (exitCode, output) = await RunGitCommandAsync(workspaceRoot, "push");
        return (exitCode == 0, output.Trim());
    }

    public async Task<GitCommitDetail?> GetCommitDetailAsync(string workspaceRoot, string commitHash)
    {
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, $"show --numstat --format=format:%H%x7c%h%x7c%an%x7c%ae%x7c%ad%x7c%s%n%b%n---AGDU_DIVIDER--- {EscapeArg(commitHash)}");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return null;

        var parts = stdout.Split("---AGDU_DIVIDER---", 2);
        string headerAndBody = parts[0].Trim();
        string numstatPart = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        using var reader = new StringReader(headerAndBody);
        string? firstLine = reader.ReadLine();
        if (string.IsNullOrEmpty(firstLine)) return null;

        var tokens = firstLine.Split('|');
        if (tokens.Length < 6) return null;

        string fullHash = tokens[0].Trim();
        string shortHash = tokens[1].Trim();
        string author = tokens[2].Trim();
        string email = tokens[3].Trim();
        string date = tokens[4].Trim();
        string subject = tokens[5].Trim();

        string body = reader.ReadToEnd().Trim();

        // Parse numstat lines
        var files = new List<GitCommitFileChange>();
        if (!string.IsNullOrWhiteSpace(numstatPart))
        {
            using var numReader = new StringReader(numstatPart);
            string? fLine;
            while ((fLine = numReader.ReadLine()) != null)
            {
                var fTokens = fLine.Split('\t');
                if (fTokens.Length >= 3)
                {
                    int.TryParse(fTokens[0], out int added);
                    int.TryParse(fTokens[1], out int deleted);
                    string path = fTokens[2].Trim();
                    files.Add(new GitCommitFileChange(path, GitFileStatusType.Modified, added, deleted));
                }
            }
        }

        return new GitCommitDetail(
            fullHash,
            shortHash,
            author,
            email,
            date,
            subject,
            body,
            Array.Empty<string>(),
            files
        );
    }

    public async Task<string?> GetHeadFileContentAsync(string workspaceRoot, string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        var (exitCode, stdout) = await RunGitCommandAsync(workspaceRoot, $"show \"HEAD:{normalized}\"");
        return exitCode == 0 ? stdout : null;
    }

    public DiffFileModel ParseDiff(string rawDiff)
    {
        var model = new DiffFileModel();
        if (string.IsNullOrWhiteSpace(rawDiff))
            return model;

        using var reader = new StringReader(rawDiff);
        string? line;
        DiffHunk? currentHunk = null;
        int oldLine = 0;
        int newLine = 0;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("diff --git"))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 4)
                {
                    model.OldPath = parts[2].StartsWith("a/") ? parts[2][2..] : parts[2];
                    model.NewPath = parts[3].StartsWith("b/") ? parts[3][2..] : parts[3];
                }
                continue;
            }

            if (line.StartsWith("@@"))
            {
                // @@ -start,count +start,count @@
                var match = Regex.Match(line, @"@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@");
                if (match.Success)
                {
                    int oStart = int.Parse(match.Groups[1].Value);
                    int oCount = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
                    int nStart = int.Parse(match.Groups[3].Value);
                    int nCount = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;

                    currentHunk = new DiffHunk
                    {
                        Header = line,
                        OldStart = oStart,
                        OldCount = oCount,
                        NewStart = nStart,
                        NewCount = nCount
                    };
                    model.Hunks.Add(currentHunk);
                    oldLine = oStart;
                    newLine = nStart;
                }
                continue;
            }

            if (currentHunk != null)
            {
                if (line.StartsWith("+"))
                {
                    model.Additions++;
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Kind = DiffLineKind.Addition,
                        OldLineNumber = null,
                        NewLineNumber = newLine++,
                        Text = line[1..]
                    });
                }
                else if (line.StartsWith("-"))
                {
                    model.Deletions++;
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Kind = DiffLineKind.Deletion,
                        OldLineNumber = oldLine++,
                        NewLineNumber = null,
                        Text = line[1..]
                    });
                }
                else if (line.StartsWith(" "))
                {
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Kind = DiffLineKind.Context,
                        OldLineNumber = oldLine++,
                        NewLineNumber = newLine++,
                        Text = line[1..]
                    });
                }
            }
        }

        return model;
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
