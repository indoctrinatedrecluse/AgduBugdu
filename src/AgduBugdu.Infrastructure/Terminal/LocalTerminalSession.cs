using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AgduBugdu.Infrastructure.Terminal;

public interface ITerminalSession : IDisposable
{
    event EventHandler<string>? OutputReceived;
    event EventHandler<int>? Exited;
    bool IsRunning { get; }
    void Start(string? workingDirectory = null);
    Task WriteInputAsync(string input);
    void Stop();
}

public class LocalTerminalSession : ITerminalSession
{
    private Process? _process;
    private StreamWriter? _inputWriter;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public bool IsRunning => _process != null && !_process.HasExited;

    public void Start(string? workingDirectory = null)
    {
        if (IsRunning)
            return;

        var isWindows = OperatingSystem.IsWindows();
        var shell = isWindows ? "powershell.exe" : "bash";

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = isWindows ? "-NoLogo" : "",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        _process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                OutputReceived?.Invoke(this, e.Data + Environment.NewLine);
            }
        };

        _process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                OutputReceived?.Invoke(this, e.Data + Environment.NewLine);
            }
        };

        _process.Exited += (s, e) =>
        {
            var code = _process?.ExitCode ?? 0;
            Exited?.Invoke(this, code);
        };

        _process.Start();
        _inputWriter = _process.StandardInput;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        OutputReceived?.Invoke(this, $"[Terminal Started: {shell} in {startInfo.WorkingDirectory}]{Environment.NewLine}");
    }

    public async Task WriteInputAsync(string input)
    {
        if (_inputWriter != null && IsRunning)
        {
            await _inputWriter.WriteLineAsync(input);
            await _inputWriter.FlushAsync();
        }
    }

    public void Stop()
    {
        try
        {
            if (IsRunning && _process != null)
            {
                _process.Kill(entireProcessTree: true);
                _process.Dispose();
                _process = null;
            }
        }
        catch (Exception)
        {
            // Ignore kill errors on teardown
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
