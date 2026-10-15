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
    void Start(string? workingDirectory = null, string? shellProgram = null, string? arguments = null);
    Task WriteInputAsync(string input);
    Task WriteRawAsync(string raw);
    void Stop();
}

public class LocalTerminalSession : ITerminalSession
{
    private Process? _process;
    private StreamWriter? _inputWriter;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public bool IsRunning => _process != null && !_process.HasExited;

    public void Start(string? workingDirectory = null, string? shellProgram = null, string? arguments = null)
    {
        if (IsRunning)
            return;

        var isWindows = OperatingSystem.IsWindows();
        var shell = !string.IsNullOrWhiteSpace(shellProgram) ? shellProgram : (isWindows ? "powershell.exe" : "bash");
        var args = arguments ?? (isWindows && shell.Contains("powershell", StringComparison.OrdinalIgnoreCase) ? "-NoLogo" : "");

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        _process.Exited += (s, e) =>
        {
            var code = _process?.ExitCode ?? 0;
            Exited?.Invoke(this, code);
        };

        _process.Start();
        _inputWriter = _process.StandardInput;
        _inputWriter.AutoFlush = true;

        _ = ReadStreamAsync(_process.StandardOutput.BaseStream);
        _ = ReadStreamAsync(_process.StandardError.BaseStream);

        OutputReceived?.Invoke(this, $"[Terminal Started: {shell} in {startInfo.WorkingDirectory}]{Environment.NewLine}");
    }

    private async Task ReadStreamAsync(Stream stream)
    {
        var buffer = new byte[4096];
        while (IsRunning)
        {
            int count = 0;
            try
            {
                count = await stream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch
            {
                break;
            }

            if (count <= 0) break;

            string text = System.Text.Encoding.UTF8.GetString(buffer, 0, count);
            OutputReceived?.Invoke(this, text);
        }
    }

    public async Task WriteInputAsync(string input)
    {
        if (_inputWriter != null && IsRunning)
        {
            await _inputWriter.WriteLineAsync(input);
            await _inputWriter.FlushAsync();
        }
    }

    public async Task WriteRawAsync(string raw)
    {
        if (_inputWriter != null && IsRunning)
        {
            await _inputWriter.WriteAsync(raw);
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
