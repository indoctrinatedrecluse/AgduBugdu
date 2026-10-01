using System.Collections.Generic;
using AgduBugdu.App.ViewModels.Documents;
using AgduBugdu.App.ViewModels.Tools;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.Docking;

public class MainDockFactory : Factory
{
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;
    private ExplorerToolViewModel? _explorerTool;
    private OutputToolViewModel? _outputTool;

    public IDocumentDock? DocumentDock => _documentDock;
    public ExplorerToolViewModel? ExplorerTool => _explorerTool;
    public OutputToolViewModel? OutputTool => _outputTool;

    public override IRootDock CreateLayout()
    {
        _explorerTool = new ExplorerToolViewModel();
        _outputTool = new OutputToolViewModel();

        var doc1 = new EditorDocumentViewModel
        {
            FileName = "Welcome.txt",
            Title = "Welcome.txt",
            TextDocument = new AvaloniaEdit.Document.TextDocument("Welcome to AgduBugdu Editor!\n\nPress Ctrl+P or Ctrl+Shift+P to open the Command Palette.\nUse File -> Open Folder... (Ctrl+K, Ctrl+O) to load a workspace.\nDouble-click any file in the Explorer to open it in a tab.\nDock panels are draggable and re-arrangeable.\n")
        };

        var leftDock = new ToolDock
        {
            Id = "LeftPane",
            Title = "Explorer",
            Proportion = 0.22,
            VisibleDockables = CreateList<IDockable>(_explorerTool),
            ActiveDockable = _explorerTool
        };

        var bottomDock = new ToolDock
        {
            Id = "BottomPane",
            Title = "Output",
            Proportion = 0.25,
            VisibleDockables = CreateList<IDockable>(_outputTool),
            ActiveDockable = _outputTool
        };

        var documentDock = new DocumentDock
        {
            Id = "DocumentsPane",
            Title = "Documents",
            Proportion = double.NaN,
            VisibleDockables = CreateList<IDockable>(doc1),
            ActiveDockable = doc1,
            CanCreateDocument = true
        };
        _documentDock = documentDock;

        var centerLayout = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                documentDock,
                new ProportionalDockSplitter(),
                bottomDock
            )
        };

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftDock,
                new ProportionalDockSplitter(),
                centerLayout
            )
        };

        var rootDock = CreateRootDock();
        rootDock.IsCollapsable = false;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.ActiveDockable = mainLayout;

        _rootDock = rootDock;
        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, System.Func<object?>>
        {
            ["Explorer"] = () => layout,
            ["Output"] = () => layout,
            ["Documents"] = () => layout
        };

        HostWindowLocator = new Dictionary<string, System.Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new Dock.Avalonia.Controls.HostWindow()
        };

        base.InitLayout(layout);
    }
}
