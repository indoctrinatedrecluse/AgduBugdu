using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels.Documents;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace AgduBugdu.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly MainDockFactory _dockFactory;

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

        HookActiveDocument();
        RegisterDefaultCommands();
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

    private void AttachDocumentEvents(EditorDocumentViewModel doc)
    {
        doc.CaretMoved += (s, e) =>
        {
            CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
        };
        CursorPosition = $"Ln {doc.Line}, Col {doc.Column}";
    }

    private void RegisterDefaultCommands()
    {
        CommandPalette.RegisterCommand("file.new", "File: New File", () => NewFile(), "Ctrl+N");
        CommandPalette.RegisterCommand("file.open", "File: Open File...", () => { _ = OpenFileAsync(); }, "Ctrl+O");
        CommandPalette.RegisterCommand("file.save", "File: Save", () => { _ = SaveFileAsync(); }, "Ctrl+S");
        CommandPalette.RegisterCommand("file.saveAs", "File: Save As...", () => { _ = SaveFileAsAsync(); }, "Ctrl+Shift+S");
        CommandPalette.RegisterCommand("view.commandpalette", "View: Open Command Palette", () => CommandPalette.Open(), "Ctrl+P");
        CommandPalette.RegisterCommand("view.toggleExplorer", "View: Toggle Explorer", () => StatusMessage = "Explorer toggled");
        CommandPalette.RegisterCommand("view.toggleOutput", "View: Toggle Output", () => StatusMessage = "Output pane toggled");
        CommandPalette.RegisterCommand("app.about", "Help: About AgduBugdu", () => StatusMessage = "AgduBugdu Editor v0.1.0 - Powered by Avalonia UI & AvalonEdit");
    }

    [RelayCommand]
    public void OpenCommandPalette()
    {
        CommandPalette.Open();
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
                return;
            }

            var doc = EditorDocumentViewModel.FromFile(filePath);
            AttachDocumentEvents(doc);
            _dockFactory.AddDockable(_dockFactory.DocumentDock, doc);
            _dockFactory.SetActiveDockable(doc);
            StatusMessage = $"Opened {doc.FileName}";
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
