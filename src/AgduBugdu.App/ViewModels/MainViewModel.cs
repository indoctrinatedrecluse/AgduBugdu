using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.Themes;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.Core;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Infrastructure.Updates;
using AgduBugdu.Plugin.MarkdownLive;
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
    private readonly Action<string> _openFileAction;
    public string? ActiveDocumentPath { get; private set; }
    public event EventHandler<DocumentEventArgs>? DocumentOpened;
    public event EventHandler<DocumentEventArgs>? DocumentSaved;
    public event EventHandler<DocumentEventArgs>? DocumentClosed;

    public AppEditorService(Action<string> openFileAction)
    {
        _openFileAction = openFileAction;
    }

    public void OpenFile(string filePath)
    {
        _openFileAction(filePath);
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

    public void SetActive(string path)
    {
        ActiveDocumentPath = path;
    }
}

public partial class MainViewModel : ViewModelBase
{
    private readonly MainDockFactory _dockFactory;
    private readonly ExtensionManager _extensionManager;
    private readonly CommandRegistry _commandRegistry;
    private readonly ToolWindowRegistry _toolRegistry;
    private readonly AppEditorService _editorService;
    private readonly AppWorkspaceService _workspaceService;
    private readonly ExtensionContext _extensionContext;
    private readonly GitHubUpdateService _updateService;
    private MarkdownPreviewViewModel? _markdownPreviewModel;

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
        _editorService = new AppEditorService(path => OpenFile(path));
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
            // Register built-in sample Markdown Live extension
            var mdExtension = new MarkdownLiveExtension();
            mdExtension.Initialize(_extensionContext);
            await mdExtension.ActivateAsync();

            // Populate Extensions Modal list
            ExtensionsModal.Extensions.Clear();
            ExtensionsModal.Extensions.Add(new ExtensionDisplayItem
            {
                Id = mdExtension.Id,
                Name = mdExtension.Name,
                Version = mdExtension.Version,
                Status = "Active",
                Description = "Provides real-time Markdown side-by-side rendering and preview."
            });

            // Sync registered plugin commands into the Command Palette
            foreach (var cmd in _commandRegistry.GetRegisteredCommands())
            {
                CommandPalette.RegisterCommand(cmd.Key, cmd.Value.Title, () => cmd.Value.Execute(), cmd.Value.Shortcut);
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
            _dockFactory.TerminalTool.ResetSizeRequested += (s, e) => ResetPaneLayout();
        }
        if (_dockFactory.OutputTool != null)
        {
            _dockFactory.OutputTool.ResetSizeRequested += (s, e) => ResetPaneLayout();
        }
    }

    private void HookExplorer()
    {
        if (_dockFactory.ExplorerTool != null)
        {
            _dockFactory.ExplorerTool.FileSelected += (s, path) =>
            {
                OpenFile(path);
            };

            _dockFactory.ExplorerTool.MarkdownPreviewRequested += (s, path) =>
            {
                OpenMarkdownLivePreview(path);
            };

            _dockFactory.ExplorerTool.OpenFolderRequested += (s, e) =>
            {
                _ = OpenFolderAsync();
            };
        }
    }

    public void OpenMarkdownLivePreview(string filePath)
    {
        OpenFile(filePath);

        if (_dockFactory.DocumentDock != null)
        {
            // Check if preview document already exists
            var existingPreview = _dockFactory.DocumentDock.VisibleDockables?
                .OfType<EditorDocumentViewModel>()
                .FirstOrDefault(d => d.Id == "markdown.preview.tab");

            if (existingPreview != null)
            {
                if (File.Exists(filePath))
                {
                    existingPreview.TextDocument = new AvaloniaEdit.Document.TextDocument(File.ReadAllText(filePath));
                }
                _dockFactory.SetActiveDockable(existingPreview);
                return;
            }

            if (_markdownPreviewModel == null)
            {
                _markdownPreviewModel = new MarkdownPreviewViewModel();
            }
            _markdownPreviewModel.UpdateDocument(filePath);

            var previewDoc = new EditorDocumentViewModel
            {
                Id = "markdown.preview.tab",
                FileName = $"Preview: {Path.GetFileName(filePath)}",
                Title = $"Preview: {Path.GetFileName(filePath)}",
                FilePath = filePath,
                TextDocument = new AvaloniaEdit.Document.TextDocument(_markdownPreviewModel.HtmlPreview)
            };

            _dockFactory.AddDockable(_dockFactory.DocumentDock, previewDoc);
            _dockFactory.SetActiveDockable(previewDoc);
            StatusMessage = $"Showing Live Preview for {Path.GetFileName(filePath)}";
        }
    }

    private void AttachDocumentEvents(EditorDocumentViewModel doc)
    {
        doc.CaretMoved += (s, e) =>
        {
            CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
        };
        CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
        if (!string.IsNullOrEmpty(doc.FilePath))
        {
            _editorService.SetActive(doc.FilePath);
        }
    }

    private void RegisterDefaultCommands()
    {
        CommandPalette.RegisterCommand("file.new", "File: New File", () => NewFile(), "Ctrl+N");
        CommandPalette.RegisterCommand("file.open", "File: Open File...", () => { _ = OpenFileAsync(); }, "Ctrl+O");
        CommandPalette.RegisterCommand("file.openFolder", "File: Open Folder...", () => { _ = OpenFolderAsync(); }, "Ctrl+K, Ctrl+O");
        CommandPalette.RegisterCommand("file.save", "File: Save", () => { _ = SaveFileAsync(); }, "Ctrl+S");
        CommandPalette.RegisterCommand("file.saveAs", "File: Save As...", () => { _ = SaveFileAsAsync(); }, "Ctrl+Shift+S");
        CommandPalette.RegisterCommand("terminal.restart", "Terminal: Restart Shell in Workspace", () => RestartTerminal(), "Ctrl+`");
        CommandPalette.RegisterCommand("terminal.clear", "Terminal: Clear Screen", () => _dockFactory.TerminalTool?.ClearTerminal());
        CommandPalette.RegisterCommand("view.resetPanes", "View: Reset Pane Layout to Default Sizes", () => ResetPaneLayout());
        CommandPalette.RegisterCommand("view.extensions", "View: Manage Extensions...", () => ShowExtensionsModal());
        CommandPalette.RegisterCommand("theme.lonelyDark", "Preferences: Color Theme - Lonely Dark", () => SetLonelyDarkTheme());
        CommandPalette.RegisterCommand("theme.solarizedContrast", "Preferences: Color Theme - Solarized Contrast", () => SetSolarizedContrastTheme());
        CommandPalette.RegisterCommand("app.checkForUpdates", "Help: Check for Updates...", () => { _ = CheckForUpdatesExplicitAsync(); });
        CommandPalette.RegisterCommand("view.commandpalette", "View: Open Command Palette", () => CommandPalette.Open(), "Ctrl+P");
        CommandPalette.RegisterCommand("view.toggleExplorer", "View: Focus Explorer", () => FocusDockable(_dockFactory.ExplorerTool));
        CommandPalette.RegisterCommand("view.toggleTerminal", "View: Focus Terminal", () => FocusDockable(_dockFactory.TerminalTool));
        CommandPalette.RegisterCommand("view.toggleOutput", "View: Focus Output", () => FocusDockable(_dockFactory.OutputTool));
        CommandPalette.RegisterCommand("app.about", "Help: About AgduBugdu", () => ShowAboutModal());
    }

    [RelayCommand]
    public void ResetPaneLayout()
    {
        _dockFactory.ResetPaneSizes();
        StatusMessage = "Panes reset to original layout sizes";
    }

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
            _dockFactory.SetActiveDockable(dockable);
        }
    }

    [RelayCommand]
    public void NewFile()
    {
        if (_dockFactory.DocumentDock != null)
        {
            var newDoc = new EditorDocumentViewModel
            {
                FileName = "Untitled.txt",
                Title = "Untitled.txt",
                TextDocument = new AvaloniaEdit.Document.TextDocument()
            };
            AttachDocumentEvents(newDoc);
            _dockFactory.AddDockable(_dockFactory.DocumentDock, newDoc);
            _dockFactory.SetActiveDockable(newDoc);
            StatusMessage = "Created new document";
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
                StatusMessage = $"Switched to {existing.FileName}";
                _editorService.NotifyOpened(filePath);
                return;
            }

            var doc = EditorDocumentViewModel.FromFile(filePath);
            AttachDocumentEvents(doc);
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







