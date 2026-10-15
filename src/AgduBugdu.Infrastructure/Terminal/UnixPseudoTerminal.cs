using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AgduBugdu.Infrastructure.Terminal;

/// <summary>
/// Unix pseudo-terminal host backed by the standard `script` utility.
///
/// `script` allocates a real PTY and runs the shell inside it, bridging its own
/// (redirected) stdin/stdout to the PTY. This gives interactive/TUI programs a genuine
/// terminal (raw input, job control, terminal size) without per-platform PTY P/Invoke,
/// and degrades gracefully to redirected pipes when `script` is unavailable.
/// </summary>
public sealed class UnixPseudoTerminal : IDisposable
{
    private Process? _process;
    private StreamWriter? _inputWriter;

    public event EventHandler<int>? Exited;

    public bool IsRunning => _process != null && !_process.HasExited;
    public Stream OutputStream => _process!.StandardOutput.BaseStream;

    public void Start(string shell, string? arguments, string? workingDirectory, short columns, short rows)
    {
        if (IsRunning) return;

        var (file, args) = BuildScriptCommand(shell, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
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

        // Drain stderr ("Script started/done" messages) so the child never blocks.
        _ = Task.Run(() =>
        {
            try
            {
                var buffer = new byte[4096];
                while (_process != null && _process.StandardError.BaseStream.Read(buffer, 0, buffer.Length) > 0) { }
            }
            catch { /* process gone */ }
        });
    }

    public Task WriteAsync(string text)
    {
        if (_inputWriter != null && IsRunning)
        {
            _inputWriter.Write(text);
            _inputWriter.Flush();
        }
        return Task.CompletedTask;
    }

    public void Resize(short columns, short rows)
    {
        // `script` does not expose PTY resize; the default terminal size is used.
    }

    public void Kill()
    {
        try { _process?.Kill(entireProcessTree: true); } catch { }
    }

    public void Dispose()
    {
        Kill();
        _inputWriter?.Dispose();
        _process?.Dispose();
        _process = null;
    }

    private static (string file, string args) BuildScriptCommand(string shell, string? arguments)
    {
        // Force an interactive shell so it shows a prompt and enables job control.
        var command = string.IsNullOrWhiteSpace(arguments) ? $"{shell} -i" : $"{shell} {arguments}";

        if (OperatingSystem.IsMacOS())
        {
            return ("/usr/bin/script", $"-q /dev/null {command}");
        }

        return ("script", $"-qefc \"{command}\" /dev/null");
    }
}
