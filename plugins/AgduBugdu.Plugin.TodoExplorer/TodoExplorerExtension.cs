using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.TodoExplorer;

public class TodoExplorerViewModel
{
    private readonly IExtensionContext _context;
    private List<TodoItem> _allItems = new();
    private string _searchQuery = string.Empty;
    private string _selectedTagFilter = "ALL";

    public string Title => "TODO & Tasks";
    public IReadOnlyList<TodoItem> FilteredItems { get; private set; } = Array.Empty<TodoItem>();

    public int TotalCount => _allItems.Count;
    public int TodoCount => _allItems.Count(x => x.Tag == "TODO");
    public int FixmeCount => _allItems.Count(x => x.Tag == "FIXME" || x.Tag == "BUG");
    public int NoteCount => _allItems.Count(x => x.Tag == "NOTE");

    public string SummaryText =>
        $"{TotalCount} tasks ({TodoCount} TODO, {FixmeCount} FIXME/BUG, {NoteCount} NOTE)";

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            ApplyFilter();
        }
    }

    public string SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            _selectedTagFilter = value;
            ApplyFilter();
        }
    }

    public event EventHandler? TasksChanged;

    public TodoExplorerViewModel(IExtensionContext context)
    {
        _context = context;
    }

    public void Refresh(string? workspacePath = null)
    {
        var root = workspacePath ?? _context.WorkspaceService.CurrentDirectory;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            _allItems = new List<TodoItem>();
            ApplyFilter();
            return;
        }

        _allItems = TodoScanner.ScanWorkspace(root);
        ApplyFilter();
    }

    public void UpdateFile(string filePath)
    {
        var root = _context.WorkspaceService.CurrentDirectory;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

        // Remove old tasks for this file
        _allItems.RemoveAll(x => string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        // Re-scan file
        var newTasks = TodoScanner.ScanFile(filePath, root);
        _allItems.AddRange(newTasks);
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        var query = _searchQuery?.Trim() ?? string.Empty;
        var tag = _selectedTagFilter?.ToUpperInvariant() ?? "ALL";

        var filtered = _allItems.Where(item =>
        {
            bool matchTag = tag switch
            {
                "TODO" => item.Tag == "TODO",
                "FIXME" => item.Tag == "FIXME" || item.Tag == "BUG",
                "NOTE" => item.Tag == "NOTE",
                _ => true
            };

            if (!matchTag) return false;

            if (string.IsNullOrEmpty(query)) return true;

            return item.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   item.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   item.Tag.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).OrderBy(x => x.RelativePath).ThenBy(x => x.LineNumber).ToList();

        FilteredItems = filtered;
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OpenItem(TodoItem item)
    {
        if (item != null && File.Exists(item.FilePath))
        {
            _context.EditorService.OpenFile(item.FilePath, item.LineNumber, item.Column);
        }
    }

    public string ToFormattedText()
    {
        if (FilteredItems.Count == 0)
        {
            return "No TODO or FIXME tasks found in current workspace.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Workspace Tasks ({SummaryText})");
        sb.AppendLine(new string('-', 80));

        string? currentFile = null;
        foreach (var item in FilteredItems)
        {
            if (item.RelativePath != currentFile)
            {
                currentFile = item.RelativePath;
                sb.AppendLine();
                sb.AppendLine($"📂 {currentFile}");
            }
            sb.AppendLine($"   Ln {item.LineNumber,4} [{item.Tag,-5}] {item.Message}");
        }

        return sb.ToString();
    }
}

public class TodoExplorerExtension : IExtension
{
    private IExtensionContext? _context;
    private TodoExplorerViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.todoexplorer";
    public string Name => "Workspace TODO & Task Explorer";
    public string Version => "1.0.0";

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new TodoExplorerViewModel(context);

        // Register Command Palette commands
        _context.Commands.RegisterCommand(
            "todo.refresh",
            "TODO: Scan Workspace for Tasks",
            () =>
            {
                _viewModel.Refresh();
                _context.Log($"TODO Explorer: Found {_viewModel.TotalCount} tasks.");
                return Task.CompletedTask;
            }
        );

        // Register Tool Window
        _context.ToolWindows.RegisterToolWindow(
            "todo.explorer.tool",
            "TODO Explorer",
            () => _viewModel,
            vm => _viewModel,
            ToolDockPosition.Left
        );

        // Auto-refresh when workspace folder opens
        _context.WorkspaceService.WorkspaceChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.NewWorkspacePath))
            {
                _viewModel.Refresh(e.NewWorkspacePath);
            }
        };

        // Live update on file save
        _context.EditorService.DocumentSaved += (s, e) =>
        {
            _viewModel.UpdateFile(e.FilePath);
        };

        _context.Log("Workspace TODO Explorer extension initialized.");
    }

    public Task ActivateAsync()
    {
        if (_viewModel != null && !string.IsNullOrEmpty(_context?.WorkspaceService.CurrentDirectory))
        {
            _viewModel.Refresh();
        }
        return Task.CompletedTask;
    }

    public Task DeactivateAsync() => Task.CompletedTask;
}
