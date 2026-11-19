using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AgduBugdu.Plugin.Debugger;

public static class DebugProcessRunner
{
    private static readonly Regex DotNetStackRegex = new(
        @"^\s*at\s+(?<func>[^\s(]+)(?:\(.*?\))?(?:\s+in\s+(?<file>.+?):line\s+(?<line>\d+))",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PythonStackRegex = new(
        @"^\s*File\s+""(?<file>[^""]+)"",\s+line\s+(?<line>\d+)(?:,\s+in\s+(?<func>[^\r\n]+))?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NodeStackRegex = new(
        @"^\s*at\s+(?:(?<func>[^\s(]+)\s+\()?(?<file>(?:[a-zA-Z]:)?[^():\r\n]+):(?<line>\d+)(?::\d+)?\)?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static (string Command, List<string> Arguments, string WorkingDirectory) ResolveTargetCommand(
        string targetFile,
        string? workspaceRoot = null)
    {
        string workDir = !string.IsNullOrEmpty(workspaceRoot) && Directory.Exists(workspaceRoot)
            ? workspaceRoot
            : Path.GetDirectoryName(targetFile) ?? Environment.CurrentDirectory;

        string ext = Path.GetExtension(targetFile).ToLowerInvariant();

        return ext switch
        {
            ".csproj" => ("dotnet", new List<string> { "run", "--project", targetFile }, workDir),
            ".cs" => File.Exists(Path.Combine(workDir, Path.GetFileNameWithoutExtension(targetFile) + ".csproj"))
                ? ("dotnet", new List<string> { "run", "--project", Path.Combine(workDir, Path.GetFileNameWithoutExtension(targetFile) + ".csproj") }, workDir)
                : ("dotnet", new List<string> { "run", targetFile }, workDir),
            ".py" => ("python", new List<string> { "-u", targetFile }, workDir),
            ".js" or ".mjs" => ("node", new List<string> { targetFile }, workDir),
            ".ts" => ("npx", new List<string> { "ts-node", targetFile }, workDir),
            ".go" => ("go", new List<string> { "run", targetFile }, workDir),
            ".rs" => ("cargo", new List<string> { "run" }, workDir),
            ".ps1" => ("powershell", new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", targetFile }, workDir),
            ".bat" or ".cmd" => ("cmd.exe", new List<string> { "/c", targetFile }, workDir),
            ".sh" => ("bash", new List<string> { targetFile }, workDir),
            ".exe" => (targetFile, new List<string>(), workDir),
            _ => (OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                  OperatingSystem.IsWindows() ? new List<string> { "/c", targetFile } : new List<string> { targetFile },
                  workDir)
        };
    }

    public static List<CallStackFrame> ParseStackTrace(string output)
    {
        var frames = new List<CallStackFrame>();
        if (string.IsNullOrWhiteSpace(output))
            return frames;

        // 1. .NET Stack Trace
        var dotNetMatches = DotNetStackRegex.Matches(output);
        foreach (Match m in dotNetMatches)
        {
            var func = m.Groups["func"].Value.Trim();
            var file = m.Groups["file"].Value.Trim();
            if (int.TryParse(m.Groups["line"].Value, out int line))
            {
                frames.Add(new CallStackFrame(func, file, line));
            }
        }

        // 2. Python Traceback
        var pyMatches = PythonStackRegex.Matches(output);
        foreach (Match m in pyMatches)
        {
            var file = m.Groups["file"].Value.Trim();
            var func = m.Groups["func"].Success && !string.IsNullOrWhiteSpace(m.Groups["func"].Value)
                ? m.Groups["func"].Value.Trim()
                : "<module>";
            if (int.TryParse(m.Groups["line"].Value, out int line))
            {
                frames.Add(new CallStackFrame(func, file, line));
            }
        }

        // 3. Node.js Stack Trace
        if (frames.Count == 0)
        {
            var nodeMatches = NodeStackRegex.Matches(output);
            foreach (Match m in nodeMatches)
            {
                var file = m.Groups["file"].Value.Trim();
                var func = m.Groups["func"].Success && !string.IsNullOrWhiteSpace(m.Groups["func"].Value)
                    ? m.Groups["func"].Value.Trim()
                    : "anonymous";
                if (int.TryParse(m.Groups["line"].Value, out int line))
                {
                    frames.Add(new CallStackFrame(func, file, line));
                }
            }
        }

        return frames;
    }

    public static DebugWatchItem EvaluateExpression(string expression, Process? activeProcess, string? targetFile)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new DebugWatchItem("(empty)", "null", "null");

        string expr = expression.Trim();
        string lower = expr.ToLowerInvariant();

        // 1. Live Process Metrics
        if (lower is "pid" or "process.id")
        {
            string val = activeProcess != null && !activeProcess.HasExited ? activeProcess.Id.ToString() : "N/A";
            return new DebugWatchItem(expr, val, "int");
        }
        if (lower is "process.name")
        {
            string val = activeProcess != null && !activeProcess.HasExited ? activeProcess.ProcessName : "N/A";
            return new DebugWatchItem(expr, val, "string");
        }
        if (lower is "memory" or "workingset" or "process.memory")
        {
            string val = "N/A";
            try
            {
                if (activeProcess != null && !activeProcess.HasExited)
                {
                    activeProcess.Refresh();
                    val = $"{(activeProcess.WorkingSet64 / (1024.0 * 1024.0)):F2} MB";
                }
            }
            catch { }
            return new DebugWatchItem(expr, val, "string");
        }
        if (lower is "threads" or "process.threads")
        {
            string val = "N/A";
            try
            {
                if (activeProcess != null && !activeProcess.HasExited)
                {
                    val = activeProcess.Threads.Count.ToString();
                }
            }
            catch { }
            return new DebugWatchItem(expr, val, "int");
        }
        if (lower is "hasexited" or "process.hasexited")
        {
            string val = activeProcess != null ? activeProcess.HasExited.ToString() : "true";
            return new DebugWatchItem(expr, val, "bool");
        }
        if (lower is "exitcode" or "process.exitcode")
        {
            string val = "N/A";
            try
            {
                if (activeProcess != null && activeProcess.HasExited)
                {
                    val = activeProcess.ExitCode.ToString();
                }
                else if (activeProcess != null)
                {
                    val = "Still running";
                }
            }
            catch { }
            return new DebugWatchItem(expr, val, "int");
        }

        // 2. Target File Info
        if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile))
        {
            if (lower is "file.name")
                return new DebugWatchItem(expr, Path.GetFileName(targetFile), "string");
            if (lower is "file.size" or "file.length")
                return new DebugWatchItem(expr, $"{new FileInfo(targetFile).Length:N0} bytes", "long");
            if (lower is "file.lines")
                return new DebugWatchItem(expr, File.ReadAllLines(targetFile).Length.ToString(), "int");
            if (lower is "file.created")
                return new DebugWatchItem(expr, File.GetCreationTime(targetFile).ToString(CultureInfo.InvariantCulture), "DateTime");
            if (lower is "file.modified")
                return new DebugWatchItem(expr, File.GetLastWriteTime(targetFile).ToString(CultureInfo.InvariantCulture), "DateTime");
        }

        // 3. Environment Variables
        string envName = expr.StartsWith("$") ? expr.Substring(1) : expr.StartsWith("env.", StringComparison.OrdinalIgnoreCase) ? expr.Substring(4) : expr;
        var envVal = Environment.GetEnvironmentVariable(envName);
        if (envVal != null)
        {
            return new DebugWatchItem(expr, envVal, "string");
        }

        // 4. Simple Math / Arithmetic Expressions (e.g. 2 + 2, 10 * 5)
        if (TryEvaluateSimpleMath(expr, out double mathResult))
        {
            return new DebugWatchItem(expr, mathResult.ToString(CultureInfo.InvariantCulture), "double");
        }

        // 5. Default Fallback
        return new DebugWatchItem(expr, activeProcess != null && !activeProcess.HasExited ? "active" : "N/A", "dynamic");
    }

    private static bool TryEvaluateSimpleMath(string expr, out double result)
    {
        result = 0;
        char[] ops = { '+', '-', '*', '/' };
        int opIdx = expr.IndexOfAny(ops);
        if (opIdx <= 0 || opIdx >= expr.Length - 1)
            return false;

        char op = expr[opIdx];
        string leftPart = expr.Substring(0, opIdx).Trim();
        string rightPart = expr.Substring(opIdx + 1).Trim();

        if (double.TryParse(leftPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double left) &&
            double.TryParse(rightPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double right))
        {
            result = op switch
            {
                '+' => left + right,
                '-' => left - right,
                '*' => left * right,
                '/' => right != 0 ? left / right : double.NaN,
                _ => 0
            };
            return true;
        }

        return false;
    }
}
