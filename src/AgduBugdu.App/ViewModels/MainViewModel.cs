using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Plugin.MarkdownLive;
using AgduBugdu.PluginContracts;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    [ObservableProperty]
    private IRootDock? _layout;

    [ObservableProperty]
    private CommandPaletteViewModel _commandPalette = new();

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _cursorPosition = "Ln 1, Col 1";

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private string _activeWorkspaceName = "No Folder Opened";

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

        HookActiveDocument();
        HookExplorer();
        RegisterDefaultCommands();

        // Load built-in and directory plugins
        _ = InitializeExtensionsAsync();
    }

    private async Task InitializeExtensionsAsync()
    {
        try
        {
            // Register built-in sample Markdown Live extension
            var mdExtension = new MarkdownLiveExtension();
            mdExtension.Initialize(_extensionContext);
            await mdExtension.ActivateAsync();

            // Sync registered plugin commands into the Command Palette
            foreach (var cmd in _commandRegistry.GetRegisteredCommands())
            {
                CommandPalette.RegisterCommand(cmd.Key, cmd.Value.Title, () => cmd.Value.Execute(), cmd.Value.Shortcut);
            }

            StatusMessage = "AgduBugdu Ready (Terminal & Markdown Live Loaded)";
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

    private void HookExplorer()
    {
        if (_dockFactory.ExplorerTool != null)
        {
            _dockFactory.ExplorerTool.FileSelected += (s, path) =>
            {
                OpenFile(path);
            };

            _dockFactory.ExplorerTool.OpenFolderRequested += (s, e) =>
            {
                _ = OpenFolderAsync();
            };
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
        CommandPalette.RegisterCommand("view.commandpalette", "View: Open Command Palette", () => CommandPalette.Open(), "Ctrl+P");
        CommandPalette.RegisterCommand("view.toggleExplorer", "View: Focus Explorer", () => FocusDockable(_dockFactory.ExplorerTool));
        CommandPalette.RegisterCommand("view.toggleTerminal", "View: Focus Terminal", () => FocusDockable(_dockFactory.TerminalTool));
        CommandPalette.RegisterCommand("view.toggleOutput", "View: Focus Output", () => FocusDockable(_dockFactory.OutputTool));
        CommandPalette.RegisterCommand("app.about", "Help: About AgduBugdu", () => StatusMessage = "AgduBugdu Editor v0.1.0 - Powered by Avalonia UI & AvalonEdit");
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
