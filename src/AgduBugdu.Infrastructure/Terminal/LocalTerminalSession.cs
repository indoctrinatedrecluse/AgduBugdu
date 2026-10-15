using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AgduBugdu.Infrastructure.Terminal;

public interface ITerminalSession : IDisposable
{
    event EventHandler<string>? OutputReceived;
    event EventHandler<int>? Exited;
    bool IsRunning { get; }
    bool EchoesInput { get; }
    void Start(string? workingDirectory = null, string? shellProgram = null, string? arguments = null);
    Task WriteInputAsync(string input);
    Task WriteRawAsync(string raw);
    void Stop();
}

public class LocalTerminalSession : ITerminalSession
{
    private Process? _process;
    private StreamWriter? _inputWriter;
    private WindowsPseudoConsole? _conPty;
    private UnixPseudoTerminal? _unixPty;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public bool IsRunning =>
        _conPty?.IsRunning == true || _unixPty?.IsRunning == true || (_process != null && !_process.HasExited);

    public bool EchoesInput => _conPty != null || _unixPty != null;

    public void Start(string? workingDirectory = null, string? shellProgram = null, string? arguments = null)
    {
        if (IsRunning) return;

        var isWindows = OperatingSystem.IsWindows();
        var shell = !string.IsNullOrWhiteSpace(shellProgram) ? shellProgram : (isWindows ? "powershell.exe" : "bash");
        var args = arguments ?? (isWindows && shell.Contains("powershell", StringComparison.OrdinalIgnoreCase) ? "-NoLogo" : "");
        var workDir = string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;

        // On Windows prefer a real ConPTY so interactive/TUI child processes receive raw
        // input; fall back to redirected pipes if ConPTY is unavailable.
        if (isWindows)
        {
            try
            {
                var conPty = new WindowsPseudoConsole();
                conPty.Exited += (s, code) => Exited?.Invoke(this, code);
                conPty.Start(BuildCommandLine(shell, args), workDir, 200, 60);
                _conPty = conPty;
                _ = Task.Run(() => ReadStreamBlocking(_conPty.OutputStream));
                OutputReceived?.Invoke(this, $"[Terminal Started: {shell} in {workDir}]{Environment.NewLine}");
                return;
            }
            catch
            {
                _conPty?.Dispose();
                _conPty = null;
                // Fall through to the pipe-based implementation.
            }
        }

        // On Unix run the shell inside a real PTY (via `script`) so interactive/TUI
        // child processes receive raw input; fall back to redirected pipes if unavailable.
        if (!isWindows)
        {
            try
            {
                var unixPty = new UnixPseudoTerminal();
                unixPty.Exited += (s, code) => Exited?.Invoke(this, code);
                unixPty.Start(shell, args, workDir, 200, 60);
                _unixPty = unixPty;
                _ = Task.Run(() => ReadStreamBlocking(_unixPty.OutputStream));
                OutputReceived?.Invoke(this, $"[Terminal Started: {shell} in {workDir}]{Environment.NewLine}");
                return;
            }
            catch
            {
                _unixPty?.Dispose();
                _unixPty = null;
                // Fall through to the pipe-based implementation.
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir
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

        OutputReceived?.Invoke(this, $"[Terminal Started: {shell} in {workDir}]{Environment.NewLine}");
    }

    private static string BuildCommandLine(string shell, string args)
    {
        return string.IsNullOrWhiteSpace(args) ? shell : $"{shell} {args}";
    }

    private async Task ReadStreamAsync(Stream stream)
    {
        var buffer = new byte[4096];
        while (IsRunning)
        {
            int count;
            try
            {
                count = await stream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch
            {
                break;
            }

            if (count <= 0) break;

            string text = Encoding.UTF8.GetString(buffer, 0, count);
            OutputReceived?.Invoke(this, text);
        }
    }

    private void ReadStreamBlocking(Stream stream)
    {
        var buffer = new byte[4096];
        while (IsRunning)
        {
            int count;
            try
            {
                count = stream.Read(buffer, 0, buffer.Length);
            }
            catch
            {
                break;
            }

            if (count <= 0) break;

            OutputReceived?.Invoke(this, Encoding.UTF8.GetString(buffer, 0, count));
        }
    }

    public async Task WriteInputAsync(string input)
    {
        if (_conPty != null)
        {
            await _conPty.WriteAsync(input + "\r");
        }
        else if (_unixPty != null)
        {
            await _unixPty.WriteAsync(input + "\r");
        }
        else if (_inputWriter != null && IsRunning)
        {
            await _inputWriter.WriteLineAsync(input);
            await _inputWriter.FlushAsync();
        }
    }

    public async Task WriteRawAsync(string raw)
    {
        if (_conPty != null)
        {
            await _conPty.WriteAsync(raw);
        }
        else if (_unixPty != null)
        {
            await _unixPty.WriteAsync(raw);
        }
        else if (_inputWriter != null && IsRunning)
        {
            await _inputWriter.WriteAsync(raw);
            await _inputWriter.FlushAsync();
        }
    }

    public void Stop()
    {
        try
        {
            if (_conPty != null)
            {
                _conPty.Kill();
                _conPty.Dispose();
                _conPty = null;
            }

            if (_unixPty != null)
            {
                _unixPty.Kill();
                _unixPty.Dispose();
                _unixPty = null;
            }

            if (IsRunning && _process != null)
            {
                _process.Kill(entireProcessTree: true);
                _process.Dispose();
                _process = null;
            }
        }
        catch
        {
            // Ignore teardown errors.
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
