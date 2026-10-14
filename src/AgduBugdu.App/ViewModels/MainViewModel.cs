using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.Themes;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Core;
using AgduBugdu.Core.Licensing;
using AgduBugdu.Core.Session;
using AgduBugdu.Core.Snippets;
using AgduBugdu.Core.Text;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Infrastructure.History;
using AgduBugdu.Infrastructure.Licensing;
using AgduBugdu.Infrastructure.Session;
using AgduBugdu.Infrastructure.Updates;
using AgduBugdu.Plugin.Cpp;
using AgduBugdu.Plugin.CSharp;
using AgduBugdu.Plugin.DataGridLive;
using AgduBugdu.Plugin.Debugger;
using AgduBugdu.Plugin.Go;
using AgduBugdu.Plugin.Java;
using AgduBugdu.Plugin.MarkdownLive;
using AgduBugdu.Plugin.PdfLive;
using AgduBugdu.Plugin.Python;
using AgduBugdu.Plugin.Rust;
using AgduBugdu.Plugin.RestClient;
using AgduBugdu.Plugin.HexViewer;
using AgduBugdu.Plugin.SqliteExplorer;
using AgduBugdu.Plugin.ColorPicker;
using AgduBugdu.Core.Settings;
using AgduBugdu.Core.Keymap;
using AgduBugdu.Infrastructure.Settings;
using AgduBugdu.Plugin.TodoExplorer;
using AgduBugdu.PluginContracts;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace AgduBugdu.App.ViewModels;

public class AppWorkspaceService : IWorkspaceService
{
    private readonly Action<string> _openFolderAction;
    public string? CurrentDirectory { get; private set; }
    public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    public AppWorkspaceService(Action<string> openFolderAction)
    {
        _openFolderAction = openFolderAction;
    }

    public void OpenFolder(string folderPath)
    {
        CurrentDirectory = folderPath;
        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(folderPath));
        _openFolderAction(folderPath);
    }

    public void SetWorkspaceDirect(string folderPath)
    {
        CurrentDirectory = folderPath;
        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(folderPath));
    }
}

public class AppEditorService : IEditorService
{
    private readonly Action<string, int, int> _openFileAction;
    private readonly Func<EditorDocumentViewModel?> _getActiveDocument;
    private readonly Action<string?, string?> _newDocumentAction;

    public string? ActiveDocumentPath { get; private set; }
    public int? ActiveLine { get; private set; } = 1;
    public int? ActiveColumn { get; private set; } = 1;

    public event EventHandler<DocumentEventArgs>? DocumentOpened;
    public event EventHandler<DocumentEventArgs>? DocumentSaved;
    public event EventHandler<DocumentEventArgs>? DocumentClosed;
    public event EventHandler<LineNavigationEventArgs>? LineNavigationRequested;

    public AppEditorService(
        Action<string, int, int> openFileAction,
        Func<EditorDocumentViewModel?> getActiveDocument,
        Action<string?, string?> newDocumentAction)
    {
        _openFileAction = openFileAction;
        _getActiveDocument = getActiveDocument;
        _newDocumentAction = newDocumentAction;
    }

    public void OpenFile(string filePath)
    {
        OpenFile(filePath, 1, 1);
    }

    public void OpenFile(string filePath, int line, int column = 1)
    {
        _openFileAction(filePath, line, column);
        LineNavigationRequested?.Invoke(this, new LineNavigationEventArgs(filePath, line, column));
    }

    public void NotifyOpened(string path)
    {
        ActiveDocumentPath = path;
        DocumentOpened?.Invoke(this, new DocumentEventArgs(path));
    }

    public void NotifySaved(string path)
    {
        DocumentSaved?.Invoke(this, new DocumentEventArgs(path));
    }

    public void NotifyClosed(string path)
    {
        DocumentClosed?.Invoke(this, new DocumentEventArgs(path));
    }

    public void SetActive(string path, int line = 1, int column = 1)
    {
        ActiveDocumentPath = path;
        ActiveLine = line;
        ActiveColumn = column;
    }

    public void InsertText(string text)
    {
        var doc = _getActiveDocument();
        doc?.InsertText(text);
    }

    public string? GetActiveDocumentText()
    {
        return _getActiveDocument()?.GetText();
    }

    public void SetActiveDocumentText(string text)
    {
        _getActiveDocument()?.SetText(text);
    }

    public void NewDocument(string? defaultFileName = null, string? initialContent = null)
    {
        _newDocumentAction(defaultFileName, initialContent);
    }
}

public class RecentEntryViewModel
{
    public string DisplayText { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public IRelayCommand Command { get; set; } = null!;
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    public void Dispose()
    {
        SaveCurrentSession();
        _dockFactory?.TerminalTool?.OnClose();
        _licenseService?.Dispose();
    }

    private readonly MainDockFactory _dockFactory;
    private readonly ExtensionManager _extensionManager;
    private readonly CommandRegistry _commandRegistry;
    private readonly ToolWindowRegistry _toolRegistry;
    private readonly AppEditorService _editorService;
    private readonly AppWorkspaceService _workspaceService;
    private readonly ExtensionContext _extensionContext;
    private readonly GitHubUpdateService _updateService;
    private readonly IRecentItemsManager _recentItemsManager;
    private readonly ISessionService _sessionService;
    private readonly List<string> _recentlyClosedDocuments = new();
    private MarkdownLiveExtension? _markdownExtension;
    private DataGridLiveExtension? _csvExtension;
    private PdfLiveExtension? _pdfExtension;

    public MainDockFactory DockFactory => _dockFactory;
    public IDebugService DebugService => _extensionContext.DebugService;
    public ILanguageService LanguageService => _extensionContext.Languages;
    public IRecentItemsManager RecentItemsManager => _recentItemsManager;
    public ISessionService SessionService => _sessionService;
    public ISettingsService SettingsService => _settingsService;

    public ObservableCollection<RecentEntryViewModel> RecentMenuItems { get; } = new();

    [ObservableProperty]
    private IRootDock? _layout;

    [ObservableProperty]
    private CommandPaletteViewModel _commandPalette = new();

    [ObservableProperty]
    private UpdateModalViewModel _updateModal;

    [ObservableProperty]
    private ExtensionsModalViewModel _extensionsModal = new();

    [ObservableProperty]
    private AboutModalViewModel _aboutModal = new();

    private readonly ILicenseService _licenseService;

    [ObservableProperty]
    private LicenseModalViewModel _licenseModal;

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private SettingsModalViewModel _settingsModal;

    public event EventHandler? KeymapSynchronized;

    public string LicenseBadgeText => $"{_licenseService.CurrentLicense.BadgeIcon} {_licenseService.CurrentLicense.DisplayTitle}";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _cursorPosition = "Ln 1, Col 1";

    [ObservableProperty]
    private string _wordWrapStatus = "Wrap: Off";

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private string _lineEndingStatus = "CRLF";

    [ObservableProperty]
    private string _indentStatus = "Spaces: 4";

    [ObservableProperty]
    private string _activeWorkspaceName = "No Folder Opened";

    [ObservableProperty]
    private string _gitBranchName = "No Git";

    [ObservableProperty]
    private bool _hasGitRepository;

    [ObservableProperty]
    private string _gitStatusSummary = string.Empty;

    [ObservableProperty]
    private int _gitChangesCount;

    [ObservableProperty]
    private string _currentThemeName = "Lonely Dark";

    // Dynamic Theme Properties for UI Binding
    [ObservableProperty]
    private IBrush _windowBackground = new SolidColorBrush(ThemeManager.LonelyDark.WindowBackground);

    [ObservableProperty]
    private IBrush _headerBackground = new SolidColorBrush(ThemeManager.LonelyDark.HeaderBackground);

    [ObservableProperty]
    private IBrush _statusBarBackground = new SolidColorBrush(ThemeManager.LonelyDark.StatusBarBackground);

    [ObservableProperty]
    private IBrush _statusBarForeground = new SolidColorBrush(ThemeManager.LonelyDark.StatusBarForeground);

    [ObservableProperty]
    private IBrush _accentBrush = new SolidColorBrush(ThemeManager.LonelyDark.AccentColor);

    [ObservableProperty]
    private IBrush _borderBrush = new SolidColorBrush(ThemeManager.LonelyDark.BorderBrush);

    [ObservableProperty]
    private IBrush _editorBackground = new SolidColorBrush(ThemeManager.LonelyDark.EditorBackground);

    [ObservableProperty]
    private IBrush _editorForeground = new SolidColorBrush(ThemeManager.LonelyDark.EditorForeground);

    [ObservableProperty]
    private IBrush _terminalBackground = new SolidColorBrush(ThemeManager.LonelyDark.TerminalBackground);

    [ObservableProperty]
    private IBrush _terminalForeground = new SolidColorBrush(ThemeManager.LonelyDark.TerminalForeground);

    public string AppTitle => $"{AppVersionInfo.AppName} v{AppVersionInfo.CurrentVersion}";

    public MainViewModel()
    {
        _dockFactory = new MainDockFactory();
        Layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(Layout);

        // Extensibility Subsystem Initialization
        _commandRegistry = new CommandRegistry();
        _toolRegistry = new ToolWindowRegistry();
        _editorService = new AppEditorService(
            (path, line, col) => OpenFile(path, line, col),
            () => GetActiveEditorDocument(),
            (name, content) => CreateNewDocument(name, content));
        _workspaceService = new AppWorkspaceService(path => OpenFolder(path));
        _extensionContext = new ExtensionContext(
            _commandRegistry,
            _toolRegistry,
            _editorService,
            _workspaceService,
            logger: (msg, lvl) => _dockFactory.OutputTool?.AppendLog(msg, lvl));
        _extensionManager = new ExtensionManager(_extensionContext);

        EditorDocumentViewModel.CommentSyntaxResolver = filePath =>
        {
            var customLang = _extensionContext.Languages.GetLanguageForFile(filePath);
            return CommentSyntax.Resolve(customLang, filePath);
        };

        _updateService = new GitHubUpdateService();
        _updateModal = new UpdateModalViewModel(_updateService);

        _licenseService = new LicenseService();
        _licenseModal = new LicenseModalViewModel(_licenseService);
        _licenseService.LicenseChanged += OnLicenseChanged;

        _recentItemsManager = new RecentItemsManager();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
        _settingsModal = new SettingsModalViewModel(_settingsService);
        _settingsModal.SettingsApplied += (s, e) => ApplySettingsToEnvironment();
        _settingsModal.KeymapChanged += (s, e) => KeymapSynchronized?.Invoke(this, EventArgs.Empty);

        // Register default productivity snippets across all supported languages
        foreach (var snip in BuiltInSnippets.GetDefaultSnippets())
        {
            _extensionContext.Languages.RegisterSnippet(snip);
        }

        ThemeManager.ThemeChanged += OnThemeChanged;
        ApplySettingsToEnvironment();

        HookActiveDocument();
        HookExplorer();
        HookSearch();
        HookGit();
        HookPanes();
        RegisterDefaultCommands();
        RefreshRecentItems();

        // Restore previous workspace session
        RestoreSession();

        // Load built-in extensions & check for updates in background
        _ = InitializeExtensionsAsync();
        _ = CheckUpdatesBackgroundAsync();
    }

    private void OnThemeChanged(object? sender, ThemeDefinition theme)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentThemeName = theme.Name;
            WindowBackground = new SolidColorBrush(theme.WindowBackground);
            HeaderBackground = new SolidColorBrush(theme.HeaderBackground);
            StatusBarBackground = new SolidColorBrush(theme.StatusBarBackground);
            StatusBarForeground = new SolidColorBrush(theme.StatusBarForeground);
            AccentBrush = new SolidColorBrush(theme.AccentColor);
            BorderBrush = new SolidColorBrush(theme.BorderBrush);
            EditorBackground = new SolidColorBrush(theme.EditorBackground);
            EditorForeground = new SolidColorBrush(theme.EditorForeground);
            TerminalBackground = new SolidColorBrush(theme.TerminalBackground);
            TerminalForeground = new SolidColorBrush(theme.TerminalForeground);
            StatusMessage = $"Theme: {theme.Name}";
        });
    }

    private async Task CheckUpdatesBackgroundAsync()
    {
        await Task.Delay(2000); // Wait 2s after launch before checking
        await UpdateModal.CheckForUpdatesSilentlyAsync();
    }

    private void OnLicenseChanged(object? sender, LicenseInfo e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            OnPropertyChanged(nameof(LicenseBadgeText));
            await RefreshExtensionsForLicenseAsync(e.IsPaid);
        });
    }

    private async Task RefreshExtensionsForLicenseAsync(bool isPaid)
    {
        string newStatus = isPaid ? "Active" : "🔒 Locked (Trial)";
        foreach (var ext in ExtensionsModal.Extensions)
        {
            ext.Status = newStatus;
        }

        if (isPaid)
        {
            StatusMessage = $"All extensions unlocked with {_licenseService.CurrentLicense.DisplayTitle}!";
            await InitializeExtensionsAsync();
        }
        else
        {
            StatusMessage = "Extensions locked in Trial mode. Activate a paid license to unlock.";
        }
    }

    [RelayCommand]
    public void OpenLicenseModal()
    {
        LicenseModal.Show();
    }

    private async Task InitializeExtensionsAsync()
    {
        try
        {
            ExtensionsModal.Extensions.Clear();
            bool isPaid = _licenseService.CurrentLicense.IsPaid;
            string extStatus = isPaid ? "Active" : "🔒 Locked (Trial)";

            // 1. Markdown Live extension
            var mdExtension = new MarkdownLiveExtension();
            mdExtension.Initialize(_extensionContext);
            if (isPaid) await mdExtension.ActivateAsync();
            _markdownExtension = mdExtension;
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = mdExtension.Id,
                Name = mdExtension.Name,
                Version = mdExtension.Version,
                Status = extStatus,
                Description = "Provides real-time Markdown side-by-side rendering and preview."
            });

            // 2. CSV / TSV Data Table extension
            var csvExtension = new DataGridLiveExtension();
            csvExtension.Initialize(_extensionContext);
            if (isPaid) await csvExtension.ActivateAsync();
            _csvExtension = csvExtension;
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = csvExtension.Id,
                Name = csvExtension.Name,
                Version = csvExtension.Version,
                Status = extStatus,
                Description = "Interactive tabular viewer and search for CSV and TSV datasets."
            });

            // 3. Live PDF Viewer & Editor extension
            var pdfExtension = new PdfLiveExtension();
            pdfExtension.Initialize(_extensionContext);
            if (isPaid) await pdfExtension.ActivateAsync();
            _pdfExtension = pdfExtension;
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = pdfExtension.Id,
                Name = pdfExtension.Name,
                Version = pdfExtension.Version,
                Status = extStatus,
                Description = "High-performance PDF reader, page layout inspector, page editor, and document merger."
            });

            // 4. Workspace TODO & Task Explorer extension
            var todoExtension = new TodoExplorerExtension();
            todoExtension.Initialize(_extensionContext);
            if (isPaid)
            {
                await todoExtension.ActivateAsync();
                if (_toolRegistry.GetRegisteredTools().TryGetValue("todo.explorer.tool", out var todoToolDesc) &&
                    todoToolDesc.ViewModelFactory() is TodoExplorerViewModel todoVm)
                {
                    _dockFactory.TodoTool?.AttachModel(todoVm);
                }
            }
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = todoExtension.Id,
                Name = todoExtension.Name,
                Version = todoExtension.Version,
                Status = extStatus,
                Description = "Scans workspace for TODO, FIXME, BUG, and NOTE comment markers."
            });

            // 5. Run & Debug Workbench extension
            var debugExtension = new DebuggerExtension();
            debugExtension.Initialize(_extensionContext);
            if (isPaid)
            {
                await debugExtension.ActivateAsync();
                if (_toolRegistry.GetRegisteredTools().TryGetValue("debug.workbench.tool", out var debugToolDesc) &&
                    debugToolDesc.ViewModelFactory() is DebuggerViewModel debugVm)
                {
                    _dockFactory.DebuggerTool?.AttachModel(debugVm);
                }
            }
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = debugExtension.Id,
                Name = debugExtension.Name,
                Version = debugExtension.Version,
                Status = extStatus,
                Description = "Interactive Run & Debugging workbench with breakpoints, variables, call stack, and console."
            });

            // 6. C/C++ Language Support
            var cppExtension = new CppLanguageExtension();
            cppExtension.Initialize(_extensionContext);
            if (isPaid) await cppExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = cppExtension.Id,
                Name = cppExtension.Name,
                Version = cppExtension.Version,
                Status = extStatus,
                Description = "Syntax highlighting, language definitions, and C/C++ code snippets."
            });

            // 7. Java Language Support
            var javaExtension = new JavaLanguageExtension();
            javaExtension.Initialize(_extensionContext);
            if (isPaid) await javaExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = javaExtension.Id,
                Name = javaExtension.Name,
                Version = javaExtension.Version,
                Status = extStatus,
                Description = "Syntax highlighting, language definitions, and Java code snippets."
            });

            // 8. Go Language Support
            var goExtension = new GoLanguageExtension();
            goExtension.Initialize(_extensionContext);
            if (isPaid) await goExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = goExtension.Id,
                Name = goExtension.Name,
                Version = goExtension.Version,
                Status = extStatus,
                Description = "Syntax highlighting, language definitions, and Go code snippets."
            });

            // 9. Rust Language Support
            var rustExtension = new RustLanguageExtension();
            rustExtension.Initialize(_extensionContext);
            if (isPaid) await rustExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = rustExtension.Id,
                Name = rustExtension.Name,
                Version = rustExtension.Version,
                Status = extStatus,
                Description = "Syntax highlighting, language definitions, and Rust code snippets."
            });

            // 10. Python Language Support
            var pythonExtension = new PythonLanguageExtension();
            pythonExtension.Initialize(_extensionContext);
            if (isPaid) await pythonExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = pythonExtension.Id,
                Name = pythonExtension.Name,
                Version = pythonExtension.Version,
                Status = extStatus,
                Description = "Syntax highlighting, language definitions, and Python code snippets."
            });

            // 11. C# / .NET Language Support
            var csharpExtension = new CSharpLanguageExtension();
            csharpExtension.Initialize(_extensionContext);
            if (isPaid) await csharpExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = csharpExtension.Id,
                Name = csharpExtension.Name,
                Version = csharpExtension.Version,
                Status = extStatus,
                Description = "Syntax highlighting, language definitions, and C# / .NET code snippets."
            });

            // 12. REST Client & API Runner
            var restExtension = new RestClientExtension();
            restExtension.Initialize(_extensionContext);
            if (isPaid) await restExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = restExtension.Id,
                Name = restExtension.Name,
                Version = restExtension.Version,
                Status = extStatus,
                Description = "Interactive HTTP/.rest request runner, formatted JSON responses, and API workbench."
            });

            // 13. Binary & Hex Inspector
            var hexExtension = new HexViewerExtension();
            hexExtension.Initialize(_extensionContext);
            if (isPaid) await hexExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = hexExtension.Id,
                Name = hexExtension.Name,
                Version = hexExtension.Version,
                Status = extStatus,
                Description = "High-performance hex viewer, Shannon entropy analysis, byte pattern search, and dump export."
            });

            // 14. SQLite Database Explorer
            var sqliteExtension = new SqliteExplorerExtension();
            sqliteExtension.Initialize(_extensionContext);
            if (isPaid) await sqliteExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = sqliteExtension.Id,
                Name = sqliteExtension.Name,
                Version = sqliteExtension.Version,
                Status = extStatus,
                Description = "SQLite database browser, table schema inspector, SQL runner, and Markdown table exporter."
            });

            // 15. Color Palette & Picker
            var colorExtension = new ColorPickerExtension();
            colorExtension.Initialize(_extensionContext);
            if (isPaid) await colorExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = colorExtension.Id,
                Name = colorExtension.Name,
                Version = colorExtension.Version,
                Status = extStatus,
                Description = "Interactive color picker, RGB/Hex/HSL converter, design swatches, and editor code insertion."
            });

            // Sync all registered plugin commands into the Command Palette
            foreach (var cmd in _commandRegistry.GetRegisteredCommands())
            {
                CommandPalette.RegisterCommand(cmd.Key, cmd.Value.Title, () => cmd.Value.Execute(), cmd.Value.Shortcut);
            }

            // Register all language snippets into the Command Palette
            foreach (var snippet in _extensionContext.Languages.GetSnippets())
            {
                CommandPalette.RegisterCommand(
                    $"snippet.{snippet.Id}",
                    $"Snippet ({snippet.LanguageId.ToUpperInvariant()}): {snippet.Prefix} - {snippet.Name}",
                    () => InsertSnippet(snippet));
            }

            StatusMessage = $"AgduBugdu v{AppVersionInfo.CurrentVersion} Ready";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Extension error: {ex.Message}";
        }
    }

    private void HookActiveDocument()
    {
        if (_dockFactory.DocumentDock != null)
        {
            if (_dockFactory.DocumentDock is System.ComponentModel.INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IDocumentDock.ActiveDockable))
                    {
                        if (_dockFactory.DocumentDock.ActiveDockable is EditorDocumentViewModel activeDoc)
                        {
                            AttachDocumentEvents(activeDoc);
                        }
                        else
                        {
                            CursorPosition = "--";
                        }
                    }
                };
            }

            if (_dockFactory.DocumentDock.ActiveDockable is EditorDocumentViewModel initialDoc)
            {
                AttachDocumentEvents(initialDoc);
            }
        }
    }

    private void HookPanes()
    {
        if (_dockFactory.TerminalTool != null)
        {
            _dockFactory.TerminalTool.MinimizeRequested += (s, e) => MinimizeBottomPane();
            _dockFactory.TerminalTool.ResetSizeRequested += (s, e) => ResetPaneLayout();
        }
        if (_dockFactory.OutputTool != null)
        {
            _dockFactory.OutputTool.MinimizeRequested += (s, e) => MinimizeBottomPane();
            _dockFactory.OutputTool.ResetSizeRequested += (s, e) => ResetPaneLayout();
        }
        if (_dockFactory.OutlineTool != null)
        {
            _dockFactory.OutlineTool.SymbolSelected += (s, sym) =>
            {
                var doc = GetActiveEditorDocument();
                if (doc != null && !string.IsNullOrEmpty(doc.FilePath))
                {
                    OpenFile(doc.FilePath, sym.LineNumber, sym.Column);
                }
            };
        }
    }

    [RelayCommand]
    public void ResetPaneLayout()
    {
        _dockFactory.ResetPaneSizes();
        StatusMessage = "Pane layout reset to default sizes";
    }

    private void HookExplorer()
    {
        if (_dockFactory.ExplorerTool != null)
        {
            _dockFactory.ExplorerTool.FileSelected += (s, path) =>
            {
                if (File.Exists(path))
                {
                    OpenFile(path);
                }
            };
            _dockFactory.ExplorerTool.MarkdownPreviewRequested += (s, path) => OpenMarkdownPreview(path);
            _dockFactory.ExplorerTool.CsvPreviewRequested += (s, path) => OpenCsvPreview(path);
            _dockFactory.ExplorerTool.PdfPreviewRequested += (s, path) => OpenPdfPreview(path);
            _dockFactory.ExplorerTool.OpenFolderRequested += (s, e) => { _ = OpenFolderAsync(); };
        }
    }

    private void HookSearch()
    {
        if (_dockFactory.SearchTool != null)
        {
            _dockFactory.SearchTool.MatchSelected += (s, match) =>
            {
                OpenFile(match.FilePath, match.LineNumber, match.ColumnNumber);
            };
        }
    }

    private void HookGit()
    {
        if (_dockFactory.GitTool != null)
        {
            _dockFactory.GitTool.FileSelected += (s, path) =>
            {
                if (File.Exists(path))
                {
                    OpenFile(path);
                }
            };
            _dockFactory.GitTool.StatusRefreshed += (s, e) =>
            {
                UpdateGitStatusBar();
            };
        }
    }

    private void UpdateGitStatusBar()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_dockFactory.GitTool != null)
            {
                HasGitRepository = _dockFactory.GitTool.HasRepository;
                GitBranchName = _dockFactory.GitTool.CurrentBranch;
                GitChangesCount = _dockFactory.GitTool.StagedChanges.Count + _dockFactory.GitTool.Changes.Count;
                GitStatusSummary = HasGitRepository ? $"{GitBranchName} ({GitChangesCount} changes)" : string.Empty;
            }
        });
    }

    public void RefreshRecentItems()
    {
        RecentMenuItems.Clear();

        var folders = _recentItemsManager.GetRecentFolders(6);
        foreach (var dir in folders)
        {
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            RecentMenuItems.Add(new RecentEntryViewModel
            {
                DisplayText = $"📁 {name} ({dir})",
                FullPath = dir,
                Command = new RelayCommand(() => OpenFolder(dir))
            });
        }

        var files = _recentItemsManager.GetRecentFiles(10);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            RecentMenuItems.Add(new RecentEntryViewModel
            {
                DisplayText = $"📄 {name} ({file})",
                FullPath = file,
                Command = new RelayCommand(() => OpenFile(file))
            });
        }

        if (RecentMenuItems.Count > 0)
        {
            RecentMenuItems.Add(new RecentEntryViewModel
            {
                DisplayText = "🧹 Clear Recently Opened",
                FullPath = string.Empty,
                Command = new RelayCommand(ClearRecentItems)
            });
        }
    }

    [RelayCommand]
    public void ClearRecentItems()
    {
        _recentItemsManager.ClearAll();
        RefreshRecentItems();
        StatusMessage = "Cleared recently opened files and workspaces";
    }

    private void AttachDocumentEvents(EditorDocumentViewModel doc)
    {
        doc.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorDocumentViewModel.Line) ||
                e.PropertyName == nameof(EditorDocumentViewModel.Column))
            {
                CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    _editorService.SetActive(doc.FilePath, doc.Line, doc.Column);
                }
            }
            else if (e.PropertyName == nameof(EditorDocumentViewModel.WordWrap))
            {
                WordWrapStatus = doc.WordWrap ? "Wrap: On" : "Wrap: Off";
            }
            else if (e.PropertyName == nameof(EditorDocumentViewModel.LineEnding))
            {
                LineEndingStatus = doc.LineEnding;
            }
            else if (e.PropertyName == nameof(EditorDocumentViewModel.TabSize) ||
                     e.PropertyName == nameof(EditorDocumentViewModel.UseSpacesForTabs))
            {
                IndentStatus = doc.UseSpacesForTabs ? $"Spaces: {doc.TabSize}" : $"Tabs: {doc.TabSize}";
            }
            else if (e.PropertyName == nameof(EditorDocumentViewModel.EncodingName))
            {
                Encoding = doc.EncodingName;
            }
        };
        CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
        WordWrapStatus = doc.WordWrap ? "Wrap: On" : "Wrap: Off";
        LineEndingStatus = doc.LineEnding;
        IndentStatus = doc.UseSpacesForTabs ? $"Spaces: {doc.TabSize}" : $"Tabs: {doc.TabSize}";
        Encoding = doc.EncodingName;
        if (!string.IsNullOrEmpty(doc.FilePath))
        {
            _editorService.SetActive(doc.FilePath, doc.Line, doc.Column);
        }

        _dockFactory.OutlineTool?.UpdateSymbols(doc.FilePath, doc.GetText());
        doc.TextDocument.TextChanged += (s, e) =>
        {
            _dockFactory.OutlineTool?.UpdateSymbols(doc.FilePath, doc.GetText());
            SaveCurrentSession();
        };
    }

    private void RegisterDefaultCommands()
    {
        CommandPalette.RegisterCommand("file.new", "File: New File", () => NewFile(), "Ctrl+N");
        CommandPalette.RegisterCommand("file.new.cpp", "File: New C++ Source File (main.cpp)", () => CreateNewDocument("main.cpp", CppLanguageExtension.Snippets[0].Body));
        CommandPalette.RegisterCommand("file.new.c", "File: New C Source File (main.c)", () => CreateNewDocument("main.c", CppLanguageExtension.Snippets[1].Body));
        CommandPalette.RegisterCommand("file.new.java", "File: New Java Source File (Main.java)", () => CreateNewDocument("Main.java", JavaLanguageExtension.Snippets[0].Body));
        CommandPalette.RegisterCommand("file.new.go", "File: New Go Source File (main.go)", () => CreateNewDocument("main.go", GoLanguageExtension.Snippets[0].Body));
        CommandPalette.RegisterCommand("file.new.rust", "File: New Rust Source File (main.rs)", () => CreateNewDocument("main.rs", RustLanguageExtension.Snippets[0].Body));
        CommandPalette.RegisterCommand("file.new.python", "File: New Python Script (main.py)", () => CreateNewDocument("main.py", PythonLanguageExtension.Snippets[0].Body));
        CommandPalette.RegisterCommand("file.new.csharp", "File: New C# Program File (Program.cs)", () => CreateNewDocument("Program.cs", CSharpLanguageExtension.Snippets[9].Body));
        CommandPalette.RegisterCommand("file.open", "File: Open File...", () => { _ = OpenFileAsync(); }, "Ctrl+O");
        CommandPalette.RegisterCommand("file.openFolder", "File: Open Folder...", () => { _ = OpenFolderAsync(); }, "Ctrl+K, Ctrl+O");
        CommandPalette.RegisterCommand("file.save", "File: Save", () => { _ = SaveFileAsync(); }, "Ctrl+S");
        CommandPalette.RegisterCommand("file.saveAs", "File: Save As...", () => { _ = SaveFileAsAsync(); }, "Ctrl+Shift+S");
        CommandPalette.RegisterCommand("terminal.restart", "Terminal: Restart Shell in Workspace", () => RestartTerminal(), "Ctrl+`");
        CommandPalette.RegisterCommand("terminal.clear", "Terminal: Clear Screen", () => _dockFactory.TerminalTool?.ClearTerminal());
        CommandPalette.RegisterCommand("edit.find", "Edit: Find in File", () => OpenFind(), "Ctrl+F");
        CommandPalette.RegisterCommand("edit.replace", "Edit: Replace in File", () => OpenReplace(), "Ctrl+H");
        CommandPalette.RegisterCommand("edit.goToLine", "Edit: Go to Line / Column", () => OpenGoToLine(), "Ctrl+G");
        CommandPalette.RegisterCommand("edit.goToMatchingBracket", "Edit: Go to Matching Bracket", () => GoToMatchingBracket(), "Ctrl+M");
        CommandPalette.RegisterCommand("edit.toggleLineComment", "Edit: Toggle Line Comment", () => ToggleLineComment(), "Ctrl+/");
        CommandPalette.RegisterCommand("edit.toggleBlockComment", "Edit: Toggle Block Comment", () => ToggleBlockComment(), "Shift+Alt+A");
        CommandPalette.RegisterCommand("edit.moveLineUp", "Edit: Move Line Up", () => MoveLineUp(), "Alt+Up");
        CommandPalette.RegisterCommand("edit.moveLineDown", "Edit: Move Line Down", () => MoveLineDown(), "Alt+Down");
        CommandPalette.RegisterCommand("edit.duplicateLineUp", "Edit: Duplicate Line Up", () => DuplicateLineUp(), "Shift+Alt+Up");
        CommandPalette.RegisterCommand("edit.duplicateLineDown", "Edit: Duplicate Line Down", () => DuplicateLineDown(), "Shift+Alt+Down");
        CommandPalette.RegisterCommand("edit.deleteLine", "Edit: Delete Line", () => DeleteLine(), "Ctrl+Shift+K");
        CommandPalette.RegisterCommand("edit.joinLines", "Edit: Join Lines", () => JoinLines(), "Ctrl+Shift+J");
        CommandPalette.RegisterCommand("edit.transformUppercase", "Edit: Transform to Uppercase", () => TransformUppercase(), "Ctrl+Shift+U");
        CommandPalette.RegisterCommand("edit.transformLowercase", "Edit: Transform to Lowercase", () => TransformLowercase(), "Ctrl+U");
        CommandPalette.RegisterCommand("view.toggleWordWrap", "View: Toggle Word Wrap", () => ToggleWordWrap(), "Alt+Z");
        CommandPalette.RegisterCommand("view.toggleBottomPanel", "View: Toggle Bottom Panel", () => ToggleBottomPane(), "Ctrl+J");
        CommandPalette.RegisterCommand("view.resetPanes", "View: Reset Panel to Default sizes", () => ResetPaneLayout());
        CommandPalette.RegisterCommand("view.extensions", "View: Manage Extensions...", () => ShowExtensionsModal());
        CommandPalette.RegisterCommand("theme.lonelyDark", "Preferences: Color Theme - Lonely Dark", () => SetLonelyDarkTheme());
        CommandPalette.RegisterCommand("theme.solarizedContrast", "Preferences: Color Theme - Solarized Contrast", () => SetSolarizedContrastTheme());
        CommandPalette.RegisterCommand("app.checkForUpdates", "Help: Check for Updates...", () => { _ = CheckForUpdatesExplicitAsync(); });
        CommandPalette.RegisterCommand("app.license", "Help: Software License & Entitlements...", () => OpenLicenseModal());
        CommandPalette.RegisterCommand("view.commandpalette", "View: Open Command Palette", () => CommandPalette.Open(), "Ctrl+P");
        CommandPalette.RegisterCommand("view.toggleExplorer", "View: Focus Explorer", () => FocusDockable(_dockFactory.ExplorerTool));
        CommandPalette.RegisterCommand("view.toggleTerminal", "View: Focus Terminal", () => FocusDockable(_dockFactory.TerminalTool));
        CommandPalette.RegisterCommand("view.toggleOutput", "View: Focus Output", () => FocusDockable(_dockFactory.OutputTool));
        CommandPalette.RegisterCommand("edit.undo", "Edit: Undo", () => Undo(), "Ctrl+Z");
        CommandPalette.RegisterCommand("edit.redo", "Edit: Redo", () => Redo(), "Ctrl+Y");
        CommandPalette.RegisterCommand("edit.cut", "Edit: Cut", () => Cut(), "Ctrl+X");
        CommandPalette.RegisterCommand("edit.copy", "Edit: Copy", () => Copy(), "Ctrl+C");
        CommandPalette.RegisterCommand("edit.paste", "Edit: Paste", () => Paste(), "Ctrl+V");
        CommandPalette.RegisterCommand("edit.selectAll", "Edit: Select All", () => SelectAll(), "Ctrl+A");
        CommandPalette.RegisterCommand("view.toggleTodo", "View: Focus TODO Tasks", () => FocusDockable(_dockFactory.TodoTool));
        CommandPalette.RegisterCommand("view.toggleDebugger", "View: Focus Run & Debug", () => FocusDockable(_dockFactory.DebuggerTool));
        CommandPalette.RegisterCommand("view.toggleNetworkSuite", "View: Focus Network & Sockets Workbench", () => FocusDockable(_dockFactory.NetworkSuiteTool));
        CommandPalette.RegisterCommand("view.toggleSearch", "View: Focus Search in Files", () => FocusSearch(), "Ctrl+Shift+F");
        CommandPalette.RegisterCommand("view.toggleGit", "View: Focus Source Control", () => FocusGit(), "Ctrl+Shift+G");
        CommandPalette.RegisterCommand("git.refresh", "Git: Refresh Status", () => { _ = _dockFactory.GitTool?.RefreshAsync(); });
        CommandPalette.RegisterCommand("git.stageAll", "Git: Stage All Changes", () => { _ = _dockFactory.GitTool?.StageAllAsync(); });
        CommandPalette.RegisterCommand("git.unstageAll", "Git: Unstage All Changes", () => { _ = _dockFactory.GitTool?.UnstageAllAsync(); });
        CommandPalette.RegisterCommand("git.commit", "Git: Commit Staged Changes", () => { _ = _dockFactory.GitTool?.CommitAsync(); });
        CommandPalette.RegisterCommand("file.clearRecent", "File: Clear Recently Opened", () => ClearRecentItems());
        CommandPalette.RegisterCommand("file.closeTab", "File: Close Active Tab", () => CloseActiveTab(), "Ctrl+W");
        CommandPalette.RegisterCommand("file.reopenClosedTab", "File: Reopen Closed Tab", () => ReopenClosedTab(), "Ctrl+Shift+T");
        CommandPalette.RegisterCommand("file.restoreSession", "File: Restore Previous Session", () => RestorePreviousSession());
        CommandPalette.RegisterCommand("view.splitRight", "View: Split Editor Right", () => SplitEditorRight(), "Ctrl+OemBackslash");
        CommandPalette.RegisterCommand("view.splitDown", "View: Split Editor Down", () => SplitEditorDown());
        CommandPalette.RegisterCommand("view.toggleSplit", "View: Toggle Split Editor", () => ToggleSplitEditor());
        CommandPalette.RegisterCommand("view.moveDocumentToOtherPane", "View: Move Tab to Other Pane", () => MoveDocumentToOtherPane(), "Ctrl+Alt+Right");
        CommandPalette.RegisterCommand("view.outline", "View: Focus Symbol Outline", () => FocusOutline(), "Ctrl+Shift+O");
        CommandPalette.RegisterCommand("view.toggleMinimap", "View: Toggle Code Minimap", () => ToggleMinimap(), "Alt+M");
        CommandPalette.RegisterCommand("view.settings", "Preferences: Open Settings (Ctrl+,)", () => OpenSettings(), "Ctrl+OemComma");
        CommandPalette.RegisterCommand("view.keymap", "Preferences: Keyboard Shortcuts", () => OpenKeymapSettings());
        CommandPalette.RegisterCommand("app.about", "Help: About AgduBugdu", () => ShowAboutModal());
    }

    [RelayCommand]
    public void OpenSettings(string? category = null)
    {
        SettingsModal.Open(category);
    }

    [RelayCommand]
    public void OpenKeymapSettings()
    {
        SettingsModal.Open("Keymap");
    }

    public IEnumerable<EditorDocumentViewModel> GetAllEditorDocuments()
    {
        if (_dockFactory.DocumentDock?.VisibleDockables != null)
        {
            foreach (var doc in _dockFactory.DocumentDock.VisibleDockables.OfType<EditorDocumentViewModel>())
                yield return doc;
        }

        if (_dockFactory.SecondaryDocumentDock?.VisibleDockables != null)
        {
            foreach (var doc in _dockFactory.SecondaryDocumentDock.VisibleDockables.OfType<EditorDocumentViewModel>())
                yield return doc;
        }
    }

    public void ApplySettingsToEnvironment()
    {
        var settings = _settingsService?.CurrentSettings;
        if (settings == null) return;

        // Apply theme
        if (settings.Theme.ThemeMode == "SolarizedContrast")
        {
            SetSolarizedContrastTheme();
        }
        else
        {
            SetLonelyDarkTheme();
        }

        // Apply custom accent color if valid
        if (!string.IsNullOrWhiteSpace(settings.Theme.AccentColorHex) && Avalonia.Media.Color.TryParse(settings.Theme.AccentColorHex, out var customAccent))
        {
            AccentBrush = new Avalonia.Media.SolidColorBrush(customAccent);
        }

        // Apply editor settings to all open documents
        foreach (var doc in GetAllEditorDocuments())
        {
            doc.ApplySettings(settings.Editor);
        }
    }

    [RelayCommand]
    public void Undo() => GetActiveEditorDocument()?.Undo();

    [RelayCommand]
    public void Redo() => GetActiveEditorDocument()?.Redo();

    [RelayCommand]
    public void Cut() => GetActiveEditorDocument()?.RequestCut();

    [RelayCommand]
    public void Copy() => GetActiveEditorDocument()?.RequestCopy();

    [RelayCommand]
    public void Paste() => GetActiveEditorDocument()?.RequestPaste();

    [RelayCommand]
    public void SelectAll() => GetActiveEditorDocument()?.RequestSelectAll();

    [RelayCommand]
    public void OpenFind()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            var sel = doc.GetSelectedTextFunc?.Invoke();
            doc.OpenFind(!string.IsNullOrEmpty(sel) ? sel : null);
        }
    }

    [RelayCommand]
    public void OpenReplace()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            var sel = doc.GetSelectedTextFunc?.Invoke();
            doc.OpenReplace(!string.IsNullOrEmpty(sel) ? sel : null);
        }
    }

    [RelayCommand]
    public void OpenGoToLine()
    {
        GetActiveEditorDocument()?.OpenGoToLine();
    }

    [RelayCommand]
    public void GoToMatchingBracket()
    {
        GetActiveEditorDocument()?.GoToMatchingBracket();
    }

    [RelayCommand]
    public void ToggleWordWrap()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.ToggleWordWrap();
            WordWrapStatus = doc.WordWrap ? "Wrap: On" : "Wrap: Off";
        }
    }

    [RelayCommand]
    public void ToggleLineComment()
    {
        GetActiveEditorDocument()?.ToggleLineComment();
    }

    [RelayCommand]
    public void ToggleBlockComment()
    {
        GetActiveEditorDocument()?.ToggleBlockComment();
    }

    [RelayCommand]
    public void MoveLineUp()
    {
        GetActiveEditorDocument()?.MoveLineUp();
    }

    [RelayCommand]
    public void MoveLineDown()
    {
        GetActiveEditorDocument()?.MoveLineDown();
    }

    [RelayCommand]
    public void DuplicateLineUp()
    {
        GetActiveEditorDocument()?.DuplicateLineUp();
    }

    [RelayCommand]
    public void DuplicateLineDown()
    {
        GetActiveEditorDocument()?.DuplicateLineDown();
    }

    [RelayCommand]
    public void DeleteLine()
    {
        GetActiveEditorDocument()?.DeleteLine();
    }

    [RelayCommand]
    public void JoinLines()
    {
        GetActiveEditorDocument()?.JoinLines();
    }

    [RelayCommand]
    public void TransformUppercase()
    {
        GetActiveEditorDocument()?.TransformUppercase();
    }

    [RelayCommand]
    public void TransformLowercase()
    {
        GetActiveEditorDocument()?.TransformLowercase();
    }

    [RelayCommand]
    public void ConvertToCrlf()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.ConvertToCrlf();
            LineEndingStatus = doc.LineEnding;
            StatusMessage = "Converted line endings to CRLF";
        }
    }

    [RelayCommand]
    public void ConvertToLf()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.ConvertToLf();
            LineEndingStatus = doc.LineEnding;
            StatusMessage = "Converted line endings to LF";
        }
    }

    [RelayCommand]
    public void SetTabSize(string sizeStr)
    {
        if (int.TryParse(sizeStr, out int size))
        {
            var doc = GetActiveEditorDocument();
            if (doc != null)
            {
                doc.SetTabSize(size);
                IndentStatus = doc.UseSpacesForTabs ? $"Spaces: {doc.TabSize}" : $"Tabs: {doc.TabSize}";
                StatusMessage = $"Set Tab Size to {size}";
            }
        }
    }

    [RelayCommand]
    public void ConvertTabsToSpaces()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.ConvertTabsToSpaces();
            IndentStatus = $"Spaces: {doc.TabSize}";
            StatusMessage = "Converted tabs to spaces";
        }
    }

    [RelayCommand]
    public void ConvertSpacesToTabs()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.ConvertSpacesToTabs();
            IndentStatus = $"Tabs: {doc.TabSize}";
            StatusMessage = "Converted spaces to tabs";
        }
    }

    [RelayCommand]
    public void SetEncoding(string encoding)
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.SetEncoding(encoding);
            Encoding = doc.EncodingName;
            StatusMessage = $"Encoding: {encoding}";
        }
    }

    [RelayCommand]
    public void MinimizeBottomPane()
    {
        _dockFactory.MinimizeBottomPane();
        StatusMessage = "Bottom panel minimized";
    }

    [RelayCommand]
    public void ToggleBottomPane()
    {
        _dockFactory.ToggleBottomPane();
        StatusMessage = _dockFactory.IsBottomPaneMinimized ? "Bottom panel minimized" : "Bottom panel restored";
    }

    [RelayCommand]
    public void FocusExplorer() => FocusDockable(_dockFactory.ExplorerTool);

    [RelayCommand]
    public void FocusSearch() => FocusDockable(_dockFactory.SearchTool);

    [RelayCommand]
    public void FocusGit() => FocusDockable(_dockFactory.GitTool);

    [RelayCommand]
    public void FocusTerminal() => FocusDockable(_dockFactory.TerminalTool);

    [RelayCommand]
    public void FocusOutput() => FocusDockable(_dockFactory.OutputTool);

    [RelayCommand]
    public void FocusTodo() => FocusDockable(_dockFactory.TodoTool);

    [RelayCommand]
    public void FocusDebugger() => FocusDockable(_dockFactory.DebuggerTool);

    [RelayCommand]
    public void FocusNetworkSuite() => FocusDockable(_dockFactory.NetworkSuiteTool);

    [RelayCommand]
    public void ShowExtensionsModal()
    {
        ExtensionsModal.Show();
    }

    [RelayCommand]
    public void ShowAboutModal()
    {
        AboutModal.Show();
    }

    [RelayCommand]
    public void SetLonelyDarkTheme()
    {
        ThemeManager.ApplyTheme(AppThemeMode.LonelyDark);
    }

    [RelayCommand]
    public void SetSolarizedContrastTheme()
    {
        ThemeManager.ApplyTheme(AppThemeMode.SolarizedContrast);
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        if (ThemeManager.CurrentTheme.Mode == AppThemeMode.LonelyDark)
        {
            ThemeManager.ApplyTheme(AppThemeMode.SolarizedContrast);
        }
        else
        {
            ThemeManager.ApplyTheme(AppThemeMode.LonelyDark);
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesExplicitAsync()
    {
        StatusMessage = "Checking for updates...";
        var result = await _updateService.CheckForUpdatesAsync();
        if (result.IsUpdateAvailable)
        {
            UpdateModal.ShowUpdate(result);
            StatusMessage = $"Update v{result.LatestVersion} available!";
        }
        else
        {
            StatusMessage = $"You're on the latest version (v{AppVersionInfo.CurrentVersion})";
        }
    }

    [RelayCommand]
    public void OpenCommandPalette()
    {
        CommandPalette.Open();
    }

    [RelayCommand]
    public void RestartTerminal()
    {
        var terminalSettings = _settingsService?.CurrentSettings.Terminal;
        var dir = !string.IsNullOrWhiteSpace(terminalSettings?.StartDirectory) && Directory.Exists(terminalSettings.StartDirectory)
            ? terminalSettings.StartDirectory
            : _workspaceService.CurrentDirectory;

        _dockFactory.TerminalTool?.Restart(dir, terminalSettings?.ShellProgram, terminalSettings?.CustomArguments);
        StatusMessage = "Terminal restarted";
    }

    [RelayCommand]
    public async Task StartOrContinueDebuggingAsync()
    {
        if (_dockFactory.DebuggerTool != null)
        {
            FocusDockable(_dockFactory.DebuggerTool);
            await _dockFactory.DebuggerTool.StartOrContinueAsync();
        }
    }

    [RelayCommand]
    public async Task StepOverDebuggingAsync()
    {
        if (_dockFactory.DebuggerTool != null)
        {
            await _dockFactory.DebuggerTool.StepOverAsync();
        }
    }

    [RelayCommand]
    public async Task StepIntoDebuggingAsync()
    {
        if (_dockFactory.DebuggerTool != null)
        {
            await _dockFactory.DebuggerTool.StepIntoAsync();
        }
    }

    [RelayCommand]
    public async Task StopDebuggingAsync()
    {
        if (_dockFactory.DebuggerTool != null)
        {
            await _dockFactory.DebuggerTool.StopAsync();
        }
    }

    private void FocusDockable(IDockable? dockable)
    {
        if (dockable != null)
        {
            if (dockable == _dockFactory.ExplorerTool || dockable == _dockFactory.SearchTool || dockable == _dockFactory.GitTool)
            {
                _dockFactory.RestoreLeftPane(dockable);
            }
            if (dockable == _dockFactory.TerminalTool || dockable == _dockFactory.OutputTool ||
                dockable == _dockFactory.TodoTool || dockable == _dockFactory.DebuggerTool)
            {
                _dockFactory.RestoreBottomPane(dockable);
            }
            _dockFactory.SetActiveDockable(dockable);
            if (dockable.Owner is IDock ownerDock)
            {
                _dockFactory.SetFocusedDockable(ownerDock, dockable);
            }
        }
    }

    [RelayCommand]
    public void NewFile()
    {
        CreateNewDocument("Untitled.txt", string.Empty);
    }

    public void CreateNewDocument(string? defaultFileName = null, string? initialContent = null)
    {
        if (_dockFactory.DocumentDock != null)
        {
            var fileName = string.IsNullOrWhiteSpace(defaultFileName) ? "Untitled.txt" : defaultFileName;
            var newDoc = new EditorDocumentViewModel
            {
                FileName = fileName,
                Title = fileName,
                TextDocument = new AvaloniaEdit.Document.TextDocument(initialContent ?? string.Empty)
            };
            if (_settingsService?.CurrentSettings != null)
            {
                newDoc.ApplySettings(_settingsService.CurrentSettings.Editor);
            }
            AttachDocumentEvents(newDoc);
            _dockFactory.AddDockable(_dockFactory.DocumentDock, newDoc);
            _dockFactory.SetActiveDockable(newDoc);
            StatusMessage = $"Created {fileName}";
        }
    }

    public EditorDocumentViewModel? GetActiveEditorDocument()
    {
        if (_dockFactory.SecondaryDocumentDock?.ActiveDockable is EditorDocumentViewModel secDoc &&
            _dockFactory.ActiveDockable == secDoc)
        {
            return secDoc;
        }

        if (_dockFactory.DocumentDock?.ActiveDockable is EditorDocumentViewModel primDoc)
        {
            return primDoc;
        }

        if (_dockFactory.SecondaryDocumentDock?.ActiveDockable is EditorDocumentViewModel secFallback)
        {
            return secFallback;
        }

        return null;
    }

    public void SaveCurrentSession()
    {
        try
        {
            var session = new WorkspaceSessionData
            {
                WorkspaceFolderPath = _workspaceService.CurrentDirectory,
                IsSplitActive = _dockFactory.IsSplitActive,
                IsBottomPaneMinimized = _dockFactory.IsBottomPaneMinimized,
                ActiveLeftToolId = _dockFactory.LeftDock?.ActiveDockable?.Id,
                ActiveBottomToolId = _dockFactory.BottomDock?.ActiveDockable?.Id
            };

            var activeDoc = GetActiveEditorDocument();
            session.ActiveDocumentPath = activeDoc?.FilePath;

            if (_dockFactory.DocumentDock?.VisibleDockables != null)
            {
                foreach (var doc in _dockFactory.DocumentDock.VisibleDockables.OfType<EditorDocumentViewModel>())
                {
                    if (!string.IsNullOrEmpty(doc.FilePath) && File.Exists(doc.FilePath))
                    {
                        session.PrimaryDocuments.Add(new DocumentSessionData
                        {
                            FilePath = doc.FilePath,
                            CaretLine = doc.Line,
                            CaretColumn = doc.Column
                        });
                    }
                }
            }

            if (_dockFactory.SecondaryDocumentDock?.VisibleDockables != null)
            {
                foreach (var doc in _dockFactory.SecondaryDocumentDock.VisibleDockables.OfType<EditorDocumentViewModel>())
                {
                    if (!string.IsNullOrEmpty(doc.FilePath) && File.Exists(doc.FilePath))
                    {
                        session.SecondaryDocuments.Add(new DocumentSessionData
                        {
                            FilePath = doc.FilePath,
                            CaretLine = doc.Line,
                            CaretColumn = doc.Column
                        });
                    }
                }
            }

            _sessionService.SaveSession(session);
        }
        catch { }
    }

    public void RestoreSession()
    {
        try
        {
            var session = _sessionService.LoadSession();
            if (session == null) return;

            if (!string.IsNullOrEmpty(session.WorkspaceFolderPath) && Directory.Exists(session.WorkspaceFolderPath))
            {
                OpenFolder(session.WorkspaceFolderPath);
            }

            if (session.IsSplitActive)
            {
                _dockFactory.SplitEditor(string.Equals(session.SplitOrientation, "Vertical", StringComparison.OrdinalIgnoreCase)
                    ? Orientation.Vertical
                    : Orientation.Horizontal);
            }

            foreach (var docData in session.PrimaryDocuments)
            {
                if (File.Exists(docData.FilePath))
                {
                    OpenFile(docData.FilePath, docData.CaretLine, docData.CaretColumn);
                }
            }

            if (session.IsSplitActive && _dockFactory.SecondaryDocumentDock != null)
            {
                foreach (var docData in session.SecondaryDocuments)
                {
                    if (File.Exists(docData.FilePath))
                    {
                        var doc = EditorDocumentViewModel.FromFile(docData.FilePath);
                        AttachDocumentEvents(doc);
                        doc.NavigateTo(docData.CaretLine, docData.CaretColumn);
                        _dockFactory.AddDockable(_dockFactory.SecondaryDocumentDock, doc);
                    }
                }
            }

            if (!string.IsNullOrEmpty(session.ActiveDocumentPath) && File.Exists(session.ActiveDocumentPath))
            {
                OpenFile(session.ActiveDocumentPath);
            }

            StatusMessage = "Restored previous workspace session.";
        }
        catch { }
    }

    [RelayCommand]
    public void SplitEditorRight()
    {
        _dockFactory.SplitEditor(Orientation.Horizontal);
        StatusMessage = "Editor split horizontally (Side-by-Side)";
        SaveCurrentSession();
    }

    [RelayCommand]
    public void SplitEditorDown()
    {
        _dockFactory.SplitEditor(Orientation.Vertical);
        StatusMessage = "Editor split vertically";
        SaveCurrentSession();
    }

    [RelayCommand]
    public void ToggleSplitEditor()
    {
        _dockFactory.ToggleSplitEditor();
        StatusMessage = _dockFactory.IsSplitActive ? "Editor split active" : "Editor split closed";
        SaveCurrentSession();
    }

    [RelayCommand]
    public void MoveDocumentToOtherPane()
    {
        _dockFactory.MoveActiveDocumentToOtherPane();
        StatusMessage = "Moved document to other pane";
        SaveCurrentSession();
    }

    [RelayCommand]
    public void FocusOutline()
    {
        _dockFactory.RestoreLeftPane(_dockFactory.OutlineTool);
        StatusMessage = "Focused Symbol Outline";
    }

    [RelayCommand]
    public void ToggleMinimap()
    {
        var doc = GetActiveEditorDocument();
        if (doc != null)
        {
            doc.ToggleMinimap();
            StatusMessage = doc.IsMinimapVisible ? "Minimap shown" : "Minimap hidden";
        }
    }

    [RelayCommand]
    public void CloseActiveTab()
    {
        var activeDoc = GetActiveEditorDocument();
        if (activeDoc == null) return;

        if (!string.IsNullOrEmpty(activeDoc.FilePath))
        {
            _recentlyClosedDocuments.Add(activeDoc.FilePath);
            _editorService.NotifyClosed(activeDoc.FilePath);
        }

        if (_dockFactory.DocumentDock?.VisibleDockables?.Contains(activeDoc) == true)
        {
            _dockFactory.DocumentDock.VisibleDockables.Remove(activeDoc);
        }
        else if (_dockFactory.SecondaryDocumentDock?.VisibleDockables?.Contains(activeDoc) == true)
        {
            _dockFactory.SecondaryDocumentDock.VisibleDockables.Remove(activeDoc);
            if (_dockFactory.SecondaryDocumentDock.VisibleDockables.Count == 0)
            {
                _dockFactory.CloseSplit();
            }
        }

        SaveCurrentSession();
    }

    [RelayCommand]
    public void ReopenClosedTab()
    {
        if (_recentlyClosedDocuments.Count > 0)
        {
            string path = _recentlyClosedDocuments[^1];
            _recentlyClosedDocuments.RemoveAt(_recentlyClosedDocuments.Count - 1);
            if (File.Exists(path))
            {
                OpenFile(path);
                StatusMessage = $"Reopened closed file: {Path.GetFileName(path)}";
            }
        }
    }

    [RelayCommand]
    public void RestorePreviousSession()
    {
        RestoreSession();
    }

    public void InsertSnippet(Snippet snippet)
    {
        var activeDoc = GetActiveEditorDocument();
        if (activeDoc != null)
        {
            activeDoc.InsertText(snippet.Body);
            StatusMessage = $"Inserted snippet: {snippet.Name}";
        }
        else
        {
            var langDef = LanguageService.GetLanguageById(snippet.LanguageId);
            var ext = langDef?.FileExtensions.FirstOrDefault() ?? ".txt";
            var fileName = $"snippet_{snippet.Prefix}{ext}";
            CreateNewDocument(fileName, snippet.Body);
            StatusMessage = $"Created {fileName} from snippet {snippet.Name}";
        }
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            var filePath = files[0].Path.LocalPath;
            OpenFile(filePath);
        }
    }

    [RelayCommand]
    public async Task OpenFolderAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Workspace Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folderPath = folders[0].Path.LocalPath;
            OpenFolder(folderPath);
        }
    }

    public void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        ActiveWorkspaceName = folderName;
        StatusMessage = $"Workspace: {folderName}";

        _workspaceService.SetWorkspaceDirect(folderPath);
        _dockFactory.ExplorerTool?.LoadFolder(folderPath);
        _dockFactory.SearchTool?.SetWorkspace(folderPath);
        _dockFactory.GitTool?.SetWorkspace(folderPath);
        _dockFactory.TerminalTool?.Restart(folderPath);

        _recentItemsManager.AddRecentFolder(folderPath);
        RefreshRecentItems();
    }

    public void OpenFile(string filePath)
    {
        OpenFile(filePath, 1, 1);
    }

    public void OpenFile(string filePath, int line, int column = 1)
    {
        if (!File.Exists(filePath))
            return;

        _recentItemsManager.AddRecentFile(filePath);
        RefreshRecentItems();

        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            OpenPdfPreview(filePath);
            return;
        }

        // Check if already opened in primary dock
        var existingPrim = _dockFactory.DocumentDock?.VisibleDockables?
            .OfType<EditorDocumentViewModel>()
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existingPrim != null)
        {
            _dockFactory.SetActiveDockable(existingPrim);
            AttachDocumentEvents(existingPrim);
            existingPrim.NavigateTo(line, column);
            StatusMessage = $"Switched to {existingPrim.FileName}";
            _editorService.NotifyOpened(filePath);
            SaveCurrentSession();
            return;
        }

        // Check if already opened in secondary dock
        var existingSec = _dockFactory.SecondaryDocumentDock?.VisibleDockables?
            .OfType<EditorDocumentViewModel>()
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existingSec != null)
        {
            _dockFactory.SetActiveDockable(existingSec);
            AttachDocumentEvents(existingSec);
            existingSec.NavigateTo(line, column);
            StatusMessage = $"Switched to {existingSec.FileName}";
            _editorService.NotifyOpened(filePath);
            SaveCurrentSession();
            return;
        }

        IDocumentDock? targetDock = _dockFactory.DocumentDock;
        if (_dockFactory.IsSplitActive && _dockFactory.SecondaryDocumentDock != null &&
            _dockFactory.ActiveDockable != null &&
            _dockFactory.SecondaryDocumentDock.VisibleDockables?.Contains(_dockFactory.ActiveDockable) == true)
        {
            targetDock = _dockFactory.SecondaryDocumentDock;
        }

        if (targetDock != null)
        {
            var doc = EditorDocumentViewModel.FromFile(filePath);
            if (_settingsService?.CurrentSettings != null)
            {
                doc.ApplySettings(_settingsService.CurrentSettings.Editor);
            }
            AttachDocumentEvents(doc);
            doc.NavigateTo(line, column);
            _dockFactory.AddDockable(targetDock, doc);
            _dockFactory.SetActiveDockable(doc);
            StatusMessage = $"Opened {doc.FileName}";
            _editorService.NotifyOpened(filePath);
            SaveCurrentSession();
        }
    }

    public void OpenMarkdownPreview(string filePath)
    {
        if (!File.Exists(filePath)) return;
        if (!_licenseService.CurrentLicense.IsPaid)
        {
            StatusMessage = "🔒 Markdown Live Preview is locked in Trial mode. Activate an Admin, Dev, or User license to unlock.";
            LicenseModal.Show();
            return;
        }

        var previewHtml = _markdownExtension?.GetPreviewDocument(filePath);
        var previewTitle = $"Preview: {Path.GetFileName(filePath)}";
        var existing = _dockFactory.DocumentDock?.VisibleDockables?
            .OfType<EditorDocumentViewModel>()
            .FirstOrDefault(d => d.Title == previewTitle);

        if (existing != null)
        {
            _dockFactory.SetActiveDockable(existing);
            return;
        }

        var textContent = previewHtml ?? File.ReadAllText(filePath);
        var previewDoc = new EditorDocumentViewModel
        {
            FileName = previewTitle,
            Title = previewTitle,
            FilePath = filePath,
            IsReadOnly = true,
            TextDocument = new AvaloniaEdit.Document.TextDocument(textContent)
        };
        AttachDocumentEvents(previewDoc);
        _dockFactory.AddDockable(_dockFactory.DocumentDock!, previewDoc);
        _dockFactory.SetActiveDockable(previewDoc);
        StatusMessage = $"Opened Markdown Preview for {Path.GetFileName(filePath)}";
    }

    public void OpenCsvPreview(string filePath)
    {
        if (!File.Exists(filePath)) return;
        if (!_licenseService.CurrentLicense.IsPaid)
        {
            StatusMessage = "🔒 Live Data Table is locked in Trial mode. Activate an Admin, Dev, or User license to unlock.";
            LicenseModal.Show();
            return;
        }

        var tableVm = new CsvTableViewModel();
        tableVm.LoadFile(filePath);
        var formatted = tableVm.ToFormattedText();
        var previewTitle = $"Data Table: {Path.GetFileName(filePath)}";
        var existing = _dockFactory.DocumentDock?.VisibleDockables?
            .OfType<EditorDocumentViewModel>()
            .FirstOrDefault(d => d.Title == previewTitle);

        if (existing != null)
        {
            _dockFactory.SetActiveDockable(existing);
            return;
        }

        var previewDoc = new EditorDocumentViewModel
        {
            FileName = previewTitle,
            Title = previewTitle,
            FilePath = filePath,
            IsReadOnly = true,
            TextDocument = new AvaloniaEdit.Document.TextDocument(formatted)
        };
        AttachDocumentEvents(previewDoc);
        _dockFactory.AddDockable(_dockFactory.DocumentDock!, previewDoc);
        _dockFactory.SetActiveDockable(previewDoc);
        StatusMessage = $"Opened Data Table for {Path.GetFileName(filePath)}";
    }

    public void OpenPdfPreview(string filePath)
    {
        if (!File.Exists(filePath)) return;
        if (!_licenseService.CurrentLicense.IsPaid)
        {
            StatusMessage = "🔒 Live PDF Viewer & Editor is locked in Trial mode. Activate an Admin, Dev, or User license to unlock.";
            LicenseModal.Show();
            return;
        }

        var previewTitle = $"PDF: {Path.GetFileName(filePath)}";
        var existing = _dockFactory.DocumentDock?.VisibleDockables?
            .OfType<EditorDocumentViewModel>()
            .FirstOrDefault(d => d.Title == previewTitle);

        if (existing != null)
        {
            _dockFactory.SetActiveDockable(existing);
            return;
        }

        var textContent = _pdfExtension?.GetPreviewDocument(filePath) 
            ?? PdfEngine.ToFormattedSummary(PdfEngine.LoadDocument(filePath), 1);

        var previewDoc = new EditorDocumentViewModel
        {
            FileName = previewTitle,
            Title = previewTitle,
            FilePath = filePath,
            IsReadOnly = true,
            TextDocument = new AvaloniaEdit.Document.TextDocument(textContent)
        };
        AttachDocumentEvents(previewDoc);
        _dockFactory.AddDockable(_dockFactory.DocumentDock!, previewDoc);
        _dockFactory.SetActiveDockable(previewDoc);
        StatusMessage = $"Opened Live PDF Viewer & Editor for {Path.GetFileName(filePath)}";
    }

    [RelayCommand]
    public async Task SaveFileAsync()
    {
        if (_dockFactory.DocumentDock?.ActiveDockable is EditorDocumentViewModel activeDoc)
        {
            if (string.IsNullOrEmpty(activeDoc.FilePath))
            {
                await SaveFileAsAsync();
            }
            else
            {
                activeDoc.Save();
                StatusMessage = $"Saved {activeDoc.FileName}";
                _editorService.NotifySaved(activeDoc.FilePath);
                _recentItemsManager.AddRecentFile(activeDoc.FilePath);
                RefreshRecentItems();
                _ = _dockFactory.GitTool?.RefreshAsync();
            }
        }
    }

    [RelayCommand]
    public async Task SaveFileAsAsync()
    {
        if (_dockFactory.DocumentDock?.ActiveDockable is EditorDocumentViewModel activeDoc)
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider == null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File As",
                SuggestedFileName = activeDoc.FileName.TrimEnd('*')
            });

            if (file != null)
            {
                var newPath = file.Path.LocalPath;
                activeDoc.SaveAs(newPath);
                StatusMessage = $"Saved as {activeDoc.FileName}";
                _editorService.NotifySaved(newPath);
                _recentItemsManager.AddRecentFile(newPath);
                RefreshRecentItems();
                _ = _dockFactory.GitTool?.RefreshAsync();
            }
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
