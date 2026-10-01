using System.Threading.Tasks;
using AgduBugdu.App.Docking;
using AgduBugdu.App.ViewModels.Documents;
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

        RegisterDefaultCommands();
    }

    private void RegisterDefaultCommands()
    {
        CommandPalette.RegisterCommand("file.new", "File: New File", () => NewFile(), "Ctrl+N");
        CommandPalette.RegisterCommand("file.open", "File: Open File...", () => { _ = OpenFileAsync(); }, "Ctrl+O");
        CommandPalette.RegisterCommand("file.save", "File: Save", () => SaveFile(), "Ctrl+S");
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
            _dockFactory.AddDockable(_dockFactory.DocumentDock, newDoc);
            _dockFactory.SetActiveDockable(newDoc);
            StatusMessage = "Created new document";
        }
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        StatusMessage = "Open File clicked";
        await Task.CompletedTask;
    }

    [RelayCommand]
    public void SaveFile()
    {
        StatusMessage = "Saved file";
    }
}
