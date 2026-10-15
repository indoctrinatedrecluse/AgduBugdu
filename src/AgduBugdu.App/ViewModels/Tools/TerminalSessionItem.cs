using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgduBugdu.Infrastructure.Terminal;

namespace AgduBugdu.App.ViewModels.Tools;

public enum ShellType
{
    PowerShell,
    CommandPrompt,
    GitBash,
    Wsl,
    Custom
}

public partial class TerminalSessionItem : ObservableObject, IDisposable
{
    private readonly ITerminalSession _session;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private ShellType _shellType;

    [ObservableProperty]
    private string _shellProgram;

    [ObservableProperty]
    private string? _shellArguments;

    [ObservableProperty]
    private string _workingDirectory;

    [ObservableProperty]
    private string _terminalOutput = string.Empty;

    [ObservableProperty]
    private string _commandInput = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _icon = "💻";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isTuiMode;

    public TerminalSessionItem(
        string title,
        ShellType shellType = ShellType.PowerShell,
        string? shellProgram = null,
        string? shellArguments = null,
        string? workingDirectory = null,
        ITerminalSession? session = null)
    {
        _title = title;
        _shellType = shellType;
        _shellProgram = shellProgram ?? ResolveShellProgram(shellType);
        _shellArguments = shellArguments;
        _workingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        _icon = ResolveIcon(shellType);

        _session = session ?? new LocalTerminalSession();
        _session.OutputReceived += (s, text) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TerminalOutput += text;
            });
        };
        _session.Exited += (s, exitCode) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsRunning = false;
                TerminalOutput += $"{Environment.NewLine}[Process exited with code {exitCode}.]{Environment.NewLine}";
            });
        };

        Start();
    }

    public void Start()
    {
        if (_session.IsRunning) return;
        IsRunning = true;
        _session.Start(WorkingDirectory, ShellProgram, ShellArguments);
    }

    public void Restart(string? workingDirectory = null)
    {
        _session.Stop();
        TerminalOutput = string.Empty;
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            WorkingDirectory = workingDirectory;
        }
        Start();
    }

    [RelayCommand]
    public async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandInput))
            return;

        var cmd = CommandInput.Trim();
        CommandInput = string.Empty;
        TerminalOutput += $"> {cmd}{Environment.NewLine}";

        if (string.Equals(cmd, "exit", StringComparison.OrdinalIgnoreCase))
        {
            _session.Stop();
            IsRunning = false;
            TerminalOutput += $"[Terminal session closed.]{Environment.NewLine}";
            return;
        }

        if (!_session.IsRunning)
        {
            Start();
        }

        await _session.WriteInputAsync(cmd);
    }

    [RelayCommand]
    public async Task SendRawInputAsync(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return;
        if (!_session.IsRunning)
        {
            Start();
        }
        await _session.WriteRawAsync(raw);
    }

    [RelayCommand]
    public void ToggleTuiMode()
    {
        IsTuiMode = !IsTuiMode;
    }

    [RelayCommand]
    public void Clear()
    {
        TerminalOutput = string.Empty;
    }

    public void Stop()
    {
        IsRunning = false;
        _session.Stop();
    }

    public void Dispose()
    {
        Stop();
        _session.Dispose();
    }

    public static string ResolveShellProgram(ShellType type)
    {
        bool isWin = OperatingSystem.IsWindows();
        return type switch
        {
            ShellType.PowerShell => isWin ? "powershell.exe" : "pwsh",
            ShellType.CommandPrompt => isWin ? "cmd.exe" : "sh",
            ShellType.GitBash => isWin && File.Exists(@"C:\Program Files\Git\bin\bash.exe")
                ? @"C:\Program Files\Git\bin\bash.exe"
                : (isWin ? "bash.exe" : "bash"),
            ShellType.Wsl => isWin ? "wsl.exe" : "bash",
            _ => isWin ? "powershell.exe" : "bash"
        };
    }

    public static string ResolveIcon(ShellType type)
    {
        return type switch
        {
            ShellType.PowerShell => "⚡",
            ShellType.CommandPrompt => "⌨️",
            ShellType.GitBash => "🌿",
            ShellType.Wsl => "🐧",
            _ => "💻"
        };
    }
}
