using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using AgduBugdu.App.Models;
using AgduBugdu.Infrastructure.Terminal;
using AgduBugdu.Plugin.Debugger;
using AgduBugdu.Plugin.TodoExplorer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class ExplorerToolViewModel : Tool
{
    [ObservableProperty]
    private string? _currentWorkspacePath;

    [ObservableProperty]
    private string _workspaceName = "NO WORKSPACE";

    [ObservableProperty]
    private bool _hasWorkspace;

    public ObservableCollection<FileSystemItem> RootItems { get; } = new();

    [ObservableProperty]
    private FileSystemItem? _selectedItem;

    public event EventHandler<string>? FileSelected;
    public event EventHandler<string>? MarkdownPreviewRequested;
    public event EventHandler<string>? CsvPreviewRequested;
    public event EventHandler? OpenFolderRequested;

    public ExplorerToolViewModel()
    {
        Id = "Explorer";
        Title = "Explorer";
    }

    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        CurrentWorkspacePath = folderPath;
        WorkspaceName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToUpperInvariant();
        HasWorkspace = true;

        RootItems.Clear();

        try
        {
            var rootDir = new DirectoryInfo(folderPath);

            foreach (var dir in rootDir.GetDirectories())
            {
                if (dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Name == ".git")
                    continue;

                var item = new FileSystemItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };
                item.Children.Add(new FileSystemItem { Name = "Loading..." });
                RootItems.Add(item);
            }

            foreach (var file in rootDir.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                RootItems.Add(new FileSystemItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false
                });
            }
        }
        catch (Exception)
        {
            // Ignore access errors
        }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ItemDoubleClicked(FileSystemItem? item)
    {
        if (item == null)
            return;

        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            if (item.IsExpanded)
            {
                item.LoadChildren();
            }
        }
        else
        {
            FileSelected?.Invoke(this, item.FullPath);
        }
    }

    [RelayCommand]
    public void OpenMarkdownPreview(FileSystemItem? item)
    {
        if (item != null && !item.IsDirectory && item.FullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            MarkdownPreviewRequested?.Invoke(this, item.FullPath);
        }
    }

    [RelayCommand]
    public void OpenCsvPreview(FileSystemItem? item)
    {
        if (item != null && !item.IsDirectory &&
            (item.FullPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
             item.FullPath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)))
        {
            CsvPreviewRequested?.Invoke(this, item.FullPath);
        }
    }
}

public partial class OutputToolViewModel : Tool
{
    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    public OutputToolViewModel()
    {
        Id = "Output";
        Title = "Output";
    }

    [RelayCommand]
    public void ResetSize()
    {
        ResetSizeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Minimize()
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }
}

public partial class TerminalToolViewModel : Tool
{
    private readonly ITerminalSession _session;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _terminalOutput = string.Empty;

    [ObservableProperty]
    private string _commandInput = string.Empty;

    public override bool OnClose()
    {
        _session?.Stop();
        return base.OnClose();
    }

    public TerminalToolViewModel()
    {
        Id = "Terminal";
        Title = "Terminal";
        _session = new LocalTerminalSession();
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
                TerminalOutput += $"{Environment.NewLine}[Process exited with code {exitCode}. Type another command or use 'Restart' to relaunch.]{Environment.NewLine}";
            });
        };
        _session.Start();
    }

    public void Restart(string? workingDirectory = null)
    {
        _session.Stop();
        TerminalOutput = string.Empty;
        _session.Start(workingDirectory);
    }

    [RelayCommand]
    public void ResetSize()
    {
        ResetSizeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Minimize()
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandInput))
            return;

        var cmd = CommandInput.Trim();
        CommandInput = string.Empty;
        TerminalOutput += $"> {cmd}{Environment.NewLine}";

        // Handle clean 'exit' command
        if (string.Equals(cmd, "exit", StringComparison.OrdinalIgnoreCase))
        {
            _session.Stop();
            TerminalOutput += $"[Terminal session ended cleanly.]{Environment.NewLine}";
            return;
        }

        // If session was exited/stopped, relaunch it transparently
        if (!_session.IsRunning)
        {
            _session.Start();
        }

        await _session.WriteInputAsync(cmd);
    }

    [RelayCommand]
    public void ClearTerminal()
    {
        TerminalOutput = string.Empty;
    }
}

public partial class TodoToolViewModel : Tool
{
    private TodoExplorerViewModel? _model;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _formattedContent = "Open a workspace folder to view tasks.";

    [ObservableProperty]
    private string _statusSummary = "0 tasks";

    public TodoToolViewModel()
    {
        Id = "TodoExplorer";
        Title = "TODO Tasks";
    }

    public void AttachModel(TodoExplorerViewModel model)
    {
        _model = model;
        _model.TasksChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateContent);
        };
        UpdateContent();
    }

    [RelayCommand]
    public void Refresh()
    {
        _model?.Refresh();
        UpdateContent();
    }

    [RelayCommand]
    public void ResetSize()
    {
        ResetSizeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Minimize()
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateContent()
    {
        if (_model != null)
        {
            StatusSummary = _model.SummaryText;
            FormattedContent = _model.ToFormattedText();
        }
    }
}

public partial class DebuggerToolViewModel : Tool
{
    private DebuggerViewModel? _model;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _formattedContent = "Debugger ready. Set breakpoints in any file with F9 and press Start (F5).";

    [ObservableProperty]
    private string _statusText = "Idle";

    public DebuggerToolViewModel()
    {
        Id = "Debugger";
        Title = "Run & Debug";
    }

    public void AttachModel(DebuggerViewModel model)
    {
        _model = model;
        _model.StateChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateContent);
        };
        UpdateContent();
    }

    [RelayCommand]
    public async Task StartOrContinueAsync()
    {
        if (_model != null) await _model.StartOrContinueAsync();
    }

    [RelayCommand]
    public async Task StepOverAsync()
    {
        if (_model != null) await _model.StepOverAsync();
    }

    [RelayCommand]
    public async Task StepIntoAsync()
    {
        if (_model != null) await _model.StepIntoAsync();
    }

    [RelayCommand]
    public async Task PauseAsync()
    {
        if (_model != null) await _model.PauseAsync();
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        if (_model != null) await _model.StopAsync();
    }

    [RelayCommand]
    public async Task RestartAsync()
    {
        if (_model != null) await _model.RestartAsync();
    }

    [RelayCommand]
    public void ClearBreakpoints()
    {
        _model?.ClearBreakpoints();
    }

    [RelayCommand]
    public void ResetSize()
    {
        ResetSizeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Minimize()
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateContent()
    {
        if (_model != null)
        {
            StatusText = _model.StatusText;
            FormattedContent = _model.ToFormattedSummary();
        }
    }
}
