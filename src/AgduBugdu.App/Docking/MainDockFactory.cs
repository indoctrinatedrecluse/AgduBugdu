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
    private ToolDock? _bottomDock;
    private ToolDock? _leftDock;
    private ExplorerToolViewModel? _explorerTool;
    private OutputToolViewModel? _outputTool;
    private TerminalToolViewModel? _terminalTool;

    public IDocumentDock? DocumentDock => _documentDock;
    public ToolDock? BottomDock => _bottomDock;
    public ToolDock? LeftDock => _leftDock;
    public ExplorerToolViewModel? ExplorerTool => _explorerTool;
    public OutputToolViewModel? OutputTool => _outputTool;
    public TerminalToolViewModel? TerminalTool => _terminalTool;

    public override IRootDock CreateLayout()
    {
        _explorerTool = new ExplorerToolViewModel();
        _outputTool = new OutputToolViewModel();
        _terminalTool = new TerminalToolViewModel();

        var doc1 = new EditorDocumentViewModel
        {
            FileName = "Welcome.txt",
            Title = "Welcome.txt",
            TextDocument = new AvaloniaEdit.Document.TextDocument("Welcome to AgduBugdu Editor!\n\nPress Ctrl+P or Ctrl+Shift+P to open the Command Palette.\nUse File -> Open Folder... (Ctrl+K, Ctrl+O) to load a workspace.\nDouble-click any file in the Explorer to open it in a tab.\nDock panels (Explorer, Terminal, Output) are draggable and re-arrangeable.\n")
        };

        var leftDock = new ToolDock
        {
            Id = "LeftPane",
            Title = "Explorer",
            Proportion = 0.22,
            VisibleDockables = CreateList<IDockable>(_explorerTool),
            ActiveDockable = _explorerTool
        };
        _leftDock = leftDock;

        var bottomDock = new ToolDock
        {
            Id = "BottomPane",
            Title = "Panel",
            Proportion = 0.28,
            VisibleDockables = CreateList<IDockable>(_terminalTool, _outputTool),
            ActiveDockable = _terminalTool
        };
        _bottomDock = bottomDock;

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

    public void ResetPaneSizes()
    {
        if (_bottomDock != null)
        {
            _bottomDock.Proportion = 0.28;
        }
        if (_leftDock != null)
        {
            _leftDock.Proportion = 0.22;
        }
        if (_documentDock != null)
        {
            _documentDock.Proportion = double.NaN;
        }
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, System.Func<object?>>
        {
            ["Explorer"] = () => layout,
            ["Output"] = () => layout,
            ["Terminal"] = () => layout,
            ["Documents"] = () => layout
        };

        HostWindowLocator = new Dictionary<string, System.Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new Dock.Avalonia.Controls.HostWindow()
        };

        base.InitLayout(layout);
    }
}
