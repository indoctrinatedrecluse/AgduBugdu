using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.App.Models;
using AgduBugdu.Core.Git;
using AgduBugdu.Core.Symbols;
using AgduBugdu.Core.Text;
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
    public event EventHandler<string>? PdfPreviewRequested;
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

    [RelayCommand]
    public void OpenPdfPreview(FileSystemItem? item)
    {
        if (item != null && !item.IsDirectory && item.FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PdfPreviewRequested?.Invoke(this, item.FullPath);
        }
    }
}

public partial class OutputToolViewModel : Tool
{
    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _outputText = "[System] AgduBugdu IDE initialized.\n[Extensibility] Extension subsystem ready.\n";

    public OutputToolViewModel()
    {
        Id = "Output";
        Title = "Output";
    }

    public void AppendLog(string message, string level = "Info")
    {
        void Action() => OutputText += $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Action);
        }
    }

    [RelayCommand]
    public void Clear()
    {
        OutputText = string.Empty;
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

    public ObservableCollection<TodoItem> FilteredItems { get; } = new();

    [ObservableProperty]
    private TodoItem? _selectedItem;

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
    public void OpenSelectedTask(TodoItem? item = null)
    {
        var target = item ?? SelectedItem;
        if (target != null)
        {
            _model?.OpenItem(target);
        }
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
            FilteredItems.Clear();
            foreach (var item in _model.FilteredItems)
            {
                FilteredItems.Add(item);
            }
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

public class WorkspaceFileGroupItem
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public string DirectoryPath => Path.GetDirectoryName(RelativePath) ?? string.Empty;
    public int MatchCount => Matches.Count;
    public bool IsExpanded { get; set; } = true;
    public ObservableCollection<WorkspaceSearchMatch> Matches { get; } = new();
}

public partial class SearchToolViewModel : Tool
{
    private string? _currentWorkspacePath;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private bool _isReplaceExpanded;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWord;

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private string _filesToInclude = string.Empty;

    [ObservableProperty]
    private string _filesToExclude = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusSummary = "Enter a search term and press Search";

    public ObservableCollection<WorkspaceFileGroupItem> Results { get; } = new();

    public event EventHandler<WorkspaceSearchMatch>? MatchSelected;

    public SearchToolViewModel()
    {
        Id = "Search";
        Title = "Search";
    }

    public void SetWorkspace(string? path)
    {
        _currentWorkspacePath = path;
    }

    [RelayCommand]
    public void ToggleReplace()
    {
        IsReplaceExpanded = !IsReplaceExpanded;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentWorkspacePath) || !Directory.Exists(_currentWorkspacePath))
        {
            StatusSummary = "No workspace folder opened.";
            return;
        }

        if (string.IsNullOrEmpty(Query))
        {
            StatusSummary = "Search query is empty.";
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching = true;
        StatusSummary = "Searching workspace...";
        Results.Clear();

        try
        {
            var options = new WorkspaceSearchOptions(
                Query,
                MatchCase,
                WholeWord,
                UseRegex,
                string.IsNullOrWhiteSpace(FilesToInclude) ? null : FilesToInclude,
                string.IsNullOrWhiteSpace(FilesToExclude) ? null : FilesToExclude
            );

            var summary = await WorkspaceSearchEngine.SearchWorkspaceAsync(_currentWorkspacePath, options, null, ct);

            foreach (var fileResult in summary.FileResults)
            {
                var group = new WorkspaceFileGroupItem
                {
                    FilePath = fileResult.FilePath,
                    RelativePath = fileResult.RelativePath,
                    IsExpanded = true
                };
                foreach (var m in fileResult.Matches)
                {
                    group.Matches.Add(m);
                }
                Results.Add(group);
            }

            StatusSummary = summary.TotalMatchesCount == 0
                ? "No matches found"
                : $"Found {summary.TotalMatchesCount} matches in {summary.MatchingFilesCount} files ({summary.Elapsed.TotalMilliseconds:F0} ms)";
        }
        catch (OperationCanceledException)
        {
            StatusSummary = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusSummary = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public async Task ReplaceAllAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentWorkspacePath) || !Directory.Exists(_currentWorkspacePath))
        {
            StatusSummary = "No workspace folder opened.";
            return;
        }

        if (string.IsNullOrEmpty(Query))
            return;

        IsSearching = true;
        StatusSummary = "Replacing across workspace...";

        try
        {
            var options = new WorkspaceSearchOptions(
                Query,
                MatchCase,
                WholeWord,
                UseRegex,
                string.IsNullOrWhiteSpace(FilesToInclude) ? null : FilesToInclude,
                string.IsNullOrWhiteSpace(FilesToExclude) ? null : FilesToExclude
            );

            var (totalReplacements, filesModified) = await WorkspaceSearchEngine.ReplaceAllAcrossWorkspaceAsync(
                _currentWorkspacePath,
                options,
                ReplaceText ?? string.Empty
            );

            StatusSummary = $"Replaced {totalReplacements} occurrences across {filesModified} files.";
            await SearchAsync();
        }
        catch (Exception ex)
        {
            StatusSummary = $"Replace error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public void Clear()
    {
        Query = string.Empty;
        ReplaceText = string.Empty;
        Results.Clear();
        StatusSummary = "Search cleared.";
    }

    [RelayCommand]
    public void MatchDoubleClicked(WorkspaceSearchMatch? match)
    {
        if (match != null)
        {
            MatchSelected?.Invoke(this, match);
        }
    }
}

public partial class GitToolViewModel : Tool
{
    private readonly IGitService _gitService;
    private string? _currentWorkspacePath;

    [ObservableProperty]
    private string _currentBranch = "detached";

    [ObservableProperty]
    private bool _hasRepository;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private string _diffOutput = string.Empty;

    public ObservableCollection<GitFileItem> StagedChanges { get; } = new();
    public ObservableCollection<GitFileItem> Changes { get; } = new();

    public event EventHandler<string>? FileSelected;
    public event EventHandler? StatusRefreshed;

    public GitToolViewModel(IGitService? gitService = null)
    {
        Id = "SourceControl";
        Title = "Source Control";
        _gitService = gitService ?? new GitService();
    }

    public void SetWorkspace(string? path)
    {
        _currentWorkspacePath = path;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentWorkspacePath) || !Directory.Exists(_currentWorkspacePath))
        {
            HasRepository = false;
            CurrentBranch = "none";
            StagedChanges.Clear();
            Changes.Clear();
            StatusText = "No workspace opened";
            return;
        }

        IsBusy = true;
        try
        {
            var status = await _gitService.GetStatusAsync(_currentWorkspacePath);
            HasRepository = status.IsGitRepository;

            if (status.IsGitRepository)
            {
                CurrentBranch = status.CurrentBranch ?? "HEAD";
                StagedChanges.Clear();
                foreach (var f in status.StagedFiles)
                {
                    StagedChanges.Add(f);
                }

                Changes.Clear();
                foreach (var f in status.UnstagedFiles.Concat(status.UntrackedFiles))
                {
                    Changes.Add(f);
                }

                StatusText = $"{status.TotalChangesCount} changes ({CurrentBranch})";
            }
            else
            {
                CurrentBranch = "not a git repo";
                StagedChanges.Clear();
                Changes.Clear();
                StatusText = "Folder is not a Git repository";
            }
            StatusRefreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = $"Git error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task StageFileAsync(GitFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_currentWorkspacePath)) return;
        IsBusy = true;
        try
        {
            await _gitService.StageFileAsync(_currentWorkspacePath, item.RelativePath);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task StageAllAsync()
    {
        if (string.IsNullOrEmpty(_currentWorkspacePath)) return;
        IsBusy = true;
        try
        {
            await _gitService.StageAllAsync(_currentWorkspacePath);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UnstageFileAsync(GitFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_currentWorkspacePath)) return;
        IsBusy = true;
        try
        {
            await _gitService.UnstageFileAsync(_currentWorkspacePath, item.RelativePath);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UnstageAllAsync()
    {
        if (string.IsNullOrEmpty(_currentWorkspacePath)) return;
        IsBusy = true;
        try
        {
            await _gitService.UnstageAllAsync(_currentWorkspacePath);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DiscardChangesAsync(GitFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_currentWorkspacePath)) return;
        IsBusy = true;
        try
        {
            await _gitService.DiscardChangesAsync(_currentWorkspacePath, item.RelativePath);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CommitAsync()
    {
        if (string.IsNullOrEmpty(_currentWorkspacePath) || string.IsNullOrWhiteSpace(CommitMessage))
        {
            StatusText = "Please enter a commit message.";
            return;
        }

        if (StagedChanges.Count == 0)
        {
            if (Changes.Count > 0)
            {
                await _gitService.StageAllAsync(_currentWorkspacePath);
            }
            else
            {
                StatusText = "Nothing to commit.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            var res = await _gitService.CommitAsync(_currentWorkspacePath, CommitMessage.Trim());
            if (res.Success)
            {
                CommitMessage = string.Empty;
                DiffOutput = string.Empty;
                StatusText = $"Committed: {res.CommitHash ?? "OK"}";
                await RefreshAsync();
            }
            else
            {
                StatusText = $"Commit failed: {res.Error}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ShowDiffAsync(GitFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(_currentWorkspacePath)) return;
        DiffOutput = await _gitService.GetDiffAsync(_currentWorkspacePath, item.RelativePath, item.IsStaged);
        FileSelected?.Invoke(this, item.FilePath);
    }
}

public partial class OutlineToolViewModel : Tool
{
    [ObservableProperty]
    private string _activeFileName = "No Document";

    [ObservableProperty]
    private string _filterQuery = string.Empty;

    public ObservableCollection<CodeSymbol> AllSymbols { get; } = new();
    public ObservableCollection<CodeSymbol> FilteredSymbols { get; } = new();

    public event EventHandler<CodeSymbol>? SymbolSelected;

    public OutlineToolViewModel()
    {
        Id = "Outline";
        Title = "Outline";
    }

    public void UpdateSymbols(string? filePath, string? content)
    {
        AllSymbols.Clear();
        FilteredSymbols.Clear();

        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(content))
        {
            ActiveFileName = "No Document";
            return;
        }

        ActiveFileName = Path.GetFileName(filePath);
        var parsed = CodeSymbolParser.ParseSymbols(content, filePath);
        foreach (var sym in parsed)
        {
            AllSymbols.Add(sym);
        }

        ApplyFilter();
    }

    partial void OnFilterQueryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredSymbols.Clear();
        string q = (FilterQuery ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var sym in AllSymbols)
        {
            if (string.IsNullOrEmpty(q) ||
                sym.Name.ToLowerInvariant().Contains(q) ||
                sym.Kind.ToString().ToLowerInvariant().Contains(q) ||
                sym.Signature.ToLowerInvariant().Contains(q))
            {
                FilteredSymbols.Add(sym);
            }
        }
    }

    [RelayCommand]
    public void SelectSymbol(CodeSymbol? symbol)
    {
        if (symbol != null)
        {
            SymbolSelected?.Invoke(this, symbol);
        }
    }
}

