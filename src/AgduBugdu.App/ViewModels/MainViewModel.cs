using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.Themes;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Core;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Infrastructure.Updates;
using AgduBugdu.Plugin.Cpp;
using AgduBugdu.Plugin.CSharp;
using AgduBugdu.Plugin.DataGridLive;
using AgduBugdu.Plugin.Debugger;
using AgduBugdu.Plugin.Go;
using AgduBugdu.Plugin.Java;
using AgduBugdu.Plugin.MarkdownLive;
using AgduBugdu.Plugin.Python;
using AgduBugdu.Plugin.Rust;
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

public partial class MainViewModel : ObservableObject, IDisposable
{
    public void Dispose()
    {
        _dockFactory?.TerminalTool?.OnClose();
    }

    private readonly MainDockFactory _dockFactory;
    private readonly ExtensionManager _extensionManager;
    private readonly CommandRegistry _commandRegistry;
    private readonly ToolWindowRegistry _toolRegistry;
    private readonly AppEditorService _editorService;
    private readonly AppWorkspaceService _workspaceService;
    private readonly ExtensionContext _extensionContext;
    private readonly GitHubUpdateService _updateService;

    public MainDockFactory DockFactory => _dockFactory;
    public IDebugService DebugService => _extensionContext.DebugService;
    public ILanguageService LanguageService => _extensionContext.Languages;

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

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _cursorPosition = "Ln 1, Col 1";

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private string _activeWorkspaceName = "No Folder Opened";

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
        _extensionContext = new ExtensionContext(_commandRegistry, _toolRegistry, _editorService, _workspaceService);
        _extensionManager = new ExtensionManager(_extensionContext);

        _updateService = new GitHubUpdateService();
        _updateModal = new UpdateModalViewModel(_updateService);

        ThemeManager.ThemeChanged += OnThemeChanged;

        HookActiveDocument();
        HookExplorer();
        HookPanes();
        RegisterDefaultCommands();

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
            StatusMessage = $"Theme: {theme.Name}";
        });
    }

    private async Task CheckUpdatesBackgroundAsync()
    {
        await Task.Delay(2000); // Wait 2s after launch before checking
        await UpdateModal.CheckForUpdatesSilentlyAsync();
    }

    private async Task InitializeExtensionsAsync()
    {
        try
        {
            ExtensionsModal.Extensions.Clear();

            // 1. Markdown Live extension
            var mdExtension = new MarkdownLiveExtension();
            mdExtension.Initialize(_extensionContext);
            await mdExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = mdExtension.Id,
                Name = mdExtension.Name,
                Version = mdExtension.Version,
                Status = "Active",
                Description = "Provides real-time Markdown side-by-side rendering and preview."
            });

            // 2. CSV / TSV Data Table extension
            var csvExtension = new DataGridLiveExtension();
            csvExtension.Initialize(_extensionContext);
            await csvExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = csvExtension.Id,
                Name = csvExtension.Name,
                Version = csvExtension.Version,
                Status = "Active",
                Description = "Interactive tabular viewer and search for CSV and TSV datasets."
            });

            // 3. Workspace TODO & Task Explorer extension
            var todoExtension = new TodoExplorerExtension();
            todoExtension.Initialize(_extensionContext);
            await todoExtension.ActivateAsync();
            if (_toolRegistry.GetRegisteredTools().TryGetValue("todo.explorer.tool", out var todoToolDesc) &&
                todoToolDesc.ViewModelFactory() is TodoExplorerViewModel todoVm)
            {
                _dockFactory.TodoTool?.AttachModel(todoVm);
            }
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = todoExtension.Id,
                Name = todoExtension.Name,
                Version = todoExtension.Version,
                Status = "Active",
                Description = "Scans workspace for TODO, FIXME, BUG, and NOTE comment markers."
            });

            // 4. Run & Debug Workbench extension
            var debugExtension = new DebuggerExtension();
            debugExtension.Initialize(_extensionContext);
            await debugExtension.ActivateAsync();
            if (_toolRegistry.GetRegisteredTools().TryGetValue("debug.workbench.tool", out var debugToolDesc) &&
                debugToolDesc.ViewModelFactory() is DebuggerViewModel debugVm)
            {
                _dockFactory.DebuggerTool?.AttachModel(debugVm);
            }
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = debugExtension.Id,
                Name = debugExtension.Name,
                Version = debugExtension.Version,
                Status = "Active",
                Description = "Interactive Run & Debugging workbench with breakpoints, variables, call stack, and console."
            });

            // 5. C/C++ Language Support
            var cppExtension = new CppLanguageExtension();
            cppExtension.Initialize(_extensionContext);
            await cppExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = cppExtension.Id,
                Name = cppExtension.Name,
                Version = cppExtension.Version,
                Status = "Active",
                Description = "Syntax highlighting, language definitions, and C/C++ code snippets."
            });

            // 6. Java Language Support
            var javaExtension = new JavaLanguageExtension();
            javaExtension.Initialize(_extensionContext);
            await javaExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = javaExtension.Id,
                Name = javaExtension.Name,
                Version = javaExtension.Version,
                Status = "Active",
                Description = "Syntax highlighting, language definitions, and Java code snippets."
            });

            // 7. Go Language Support
            var goExtension = new GoLanguageExtension();
            goExtension.Initialize(_extensionContext);
            await goExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = goExtension.Id,
                Name = goExtension.Name,
                Version = goExtension.Version,
                Status = "Active",
                Description = "Syntax highlighting, language definitions, and Go code snippets."
            });

            // 8. Rust Language Support
            var rustExtension = new RustLanguageExtension();
            rustExtension.Initialize(_extensionContext);
            await rustExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = rustExtension.Id,
                Name = rustExtension.Name,
                Version = rustExtension.Version,
                Status = "Active",
                Description = "Syntax highlighting, language definitions, and Rust code snippets."
            });

            // 9. Python Language Support
            var pythonExtension = new PythonLanguageExtension();
            pythonExtension.Initialize(_extensionContext);
            await pythonExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = pythonExtension.Id,
                Name = pythonExtension.Name,
                Version = pythonExtension.Version,
                Status = "Active",
                Description = "Syntax highlighting, language definitions, and Python code snippets."
            });

            // 10. C# / .NET Language Support
            var csharpExtension = new CSharpLanguageExtension();
            csharpExtension.Initialize(_extensionContext);
            await csharpExtension.ActivateAsync();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = csharpExtension.Id,
                Name = csharpExtension.Name,
                Version = csharpExtension.Version,
                Status = "Active",
                Description = "Syntax highlighting, language definitions, and C# / .NET code snippets."
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
            if (_dockFactory.DocumentDock.ActiveDockable is EditorDocumentViewModel activeDoc)
            {
                AttachDocumentEvents(activeDoc);
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
        }
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
        };
        CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
        if (!string.IsNullOrEmpty(doc.FilePath))
        {
            _editorService.SetActive(doc.FilePath, doc.Line, doc.Column);
        }
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
        CommandPalette.RegisterCommand("view.toggleBottomPanel", "View: Toggle Bottom Panel", () => ToggleBottomPane(), "Ctrl+J");
        CommandPalette.RegisterCommand("view.resetPanes", "View: Reset Panel to Default sizes", () => ResetPaneLayout());
        CommandPalette.RegisterCommand("view.extensions", "View: Manage Extensions...", () => ShowExtensionsModal());
        CommandPalette.RegisterCommand("theme.lonelyDark", "Preferences: Color Theme - Lonely Dark", () => SetLonelyDarkTheme());
        CommandPalette.RegisterCommand("theme.solarizedContrast", "Preferences: Color Theme - Solarized Contrast", () => SetSolarizedContrastTheme());
        CommandPalette.RegisterCommand("app.checkForUpdates", "Help: Check for Updates...", () => { _ = CheckForUpdatesExplicitAsync(); });
        CommandPalette.RegisterCommand("view.commandpalette", "View: Open Command Palette", () => CommandPalette.Open(), "Ctrl+P");
        CommandPalette.RegisterCommand("view.toggleExplorer", "View: Focus Explorer", () => FocusDockable(_dockFactory.ExplorerTool));
        CommandPalette.RegisterCommand("view.toggleTerminal", "View: Focus Terminal", () => FocusDockable(_dockFactory.TerminalTool));
        CommandPalette.RegisterCommand("view.toggleOutput", "View: Focus Output", () => FocusDockable(_dockFactory.OutputTool));
        CommandPalette.RegisterCommand("view.toggleTodo", "View: Focus TODO Tasks", () => FocusDockable(_dockFactory.TodoTool));
        CommandPalette.RegisterCommand("view.toggleDebugger", "View: Focus Run & Debug", () => FocusDockable(_dockFactory.DebuggerTool));
        CommandPalette.RegisterCommand("app.about", "Help: About AgduBugdu", () => ShowAboutModal());
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
    public void FocusTerminal() => FocusDockable(_dockFactory.TerminalTool);

    [RelayCommand]
    public void FocusOutput() => FocusDockable(_dockFactory.OutputTool);

    [RelayCommand]
    public void FocusTodo() => FocusDockable(_dockFactory.TodoTool);

    [RelayCommand]
    public void FocusDebugger() => FocusDockable(_dockFactory.DebuggerTool);

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
    public async Task CheckForUpdatesExplicitAsync()
    {
        StatusMessage = "Checking for updates...";
        var result = await _updateService.CheckForUpdatesAsync();
        if (result.IsUpdateAvailable)
        {
            UpdateModal.ShowUpdate(result.LatestVersion, result.ReleaseNotes, result.ReleaseUrl);
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
        _dockFactory.TerminalTool?.Restart(_workspaceService.CurrentDirectory);
        StatusMessage = "Terminal restarted";
    }

    private void FocusDockable(IDockable? dockable)
    {
        if (dockable != null)
        {
            if (dockable == _dockFactory.ExplorerTool)
            {
                _dockFactory.RestoreLeftPane();
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
            AttachDocumentEvents(newDoc);
            _dockFactory.AddDockable(_dockFactory.DocumentDock, newDoc);
            _dockFactory.SetActiveDockable(newDoc);
            StatusMessage = $"Created {fileName}";
        }
    }

    public EditorDocumentViewModel? GetActiveEditorDocument()
    {
        return _dockFactory.DocumentDock?.ActiveDockable as EditorDocumentViewModel;
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
        _dockFactory.TerminalTool?.Restart(folderPath);
    }

    public void OpenFile(string filePath)
    {
        OpenFile(filePath, 1, 1);
    }

    public void OpenFile(string filePath, int line, int column = 1)
    {
        if (!File.Exists(filePath))
            return;

        if (_dockFactory.DocumentDock != null)
        {
            // Check if already opened in a tab
            var existing = _dockFactory.DocumentDock.VisibleDockables?
                .OfType<EditorDocumentViewModel>()
                .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                _dockFactory.SetActiveDockable(existing);
                AttachDocumentEvents(existing);
                existing.NavigateTo(line, column);
                StatusMessage = $"Switched to {existing.FileName}";
                _editorService.NotifyOpened(filePath);
                return;
            }

            var doc = EditorDocumentViewModel.FromFile(filePath);
            AttachDocumentEvents(doc);
            doc.NavigateTo(line, column);
            _dockFactory.AddDockable(_dockFactory.DocumentDock, doc);
            _dockFactory.SetActiveDockable(doc);
            StatusMessage = $"Opened {doc.FileName}";
            _editorService.NotifyOpened(filePath);
        }
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
