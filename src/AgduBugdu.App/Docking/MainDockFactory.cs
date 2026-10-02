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
    private ProportionalDock? _centerLayout;
    private ExplorerToolViewModel? _explorerTool;
    private OutputToolViewModel? _outputTool;
    private TerminalToolViewModel? _terminalTool;
    private TodoToolViewModel? _todoTool;
    private DebuggerToolViewModel? _debuggerTool;

    public IDocumentDock? DocumentDock => _documentDock;
    public ToolDock? BottomDock => _bottomDock;
    public ToolDock? LeftDock => _leftDock;
    public ExplorerToolViewModel? ExplorerTool => _explorerTool;
    public OutputToolViewModel? OutputTool => _outputTool;
    public TerminalToolViewModel? TerminalTool => _terminalTool;
    public TodoToolViewModel? TodoTool => _todoTool;
    public DebuggerToolViewModel? DebuggerTool => _debuggerTool;

    public override IRootDock CreateLayout()
    {
        _explorerTool = new ExplorerToolViewModel();
        _outputTool = new OutputToolViewModel();
        _terminalTool = new TerminalToolViewModel();
        _todoTool = new TodoToolViewModel();
        _debuggerTool = new DebuggerToolViewModel();

        var doc1 = new EditorDocumentViewModel
        {
            FileName = "Welcome.txt",
            Title = "Welcome.txt",
            TextDocument = new AvaloniaEdit.Document.TextDocument("Welcome to AgduBugdu Editor!\n\nPress Ctrl+P or Ctrl+Shift+P to open the Command Palette.\nUse File -> Open Folder... (Ctrl+K, Ctrl+O) to load a workspace.\nDouble-click any file in the Explorer to open it in a tab.\nDock panels (Explorer, Terminal, Output, TODO Tasks, Run & Debug) are draggable and re-arrangeable.\n")
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
            VisibleDockables = CreateList<IDockable>(_terminalTool, _outputTool, _todoTool, _debuggerTool),
            ActiveDockable = _terminalTool
        };
        _bottomDock = bottomDock;

        var documentDock = new DocumentDock
        {
            Id = "DocumentsPane",
            Title = "Documents",
            Proportion = 0.72,
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
        _centerLayout = centerLayout;

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
        if (_bottomDock != null) _bottomDock.Proportion = 0.28;
        if (_documentDock != null) _documentDock.Proportion = 0.72;
        if (_leftDock != null) _leftDock.Proportion = 0.22;
        if (_centerLayout != null) _centerLayout.Proportion = double.NaN;

        if (_centerLayout != null)
        {
            var temp = _centerLayout.VisibleDockables;
            _centerLayout.VisibleDockables = null;
            _centerLayout.VisibleDockables = temp;
        }

        if (_rootDock != null && _rootDock.ActiveDockable is ProportionalDock mainLayout)
        {
            var temp = mainLayout.VisibleDockables;
            mainLayout.VisibleDockables = null;
            mainLayout.VisibleDockables = temp;
        }
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, System.Func<object?>>
        {
            ["Explorer"] = () => _explorerTool,
            ["Output"] = () => _outputTool,
            ["Terminal"] = () => _terminalTool,
            ["TodoExplorer"] = () => _todoTool,
            ["Debugger"] = () => _debuggerTool
        };
        DockableLocator = new Dictionary<string, System.Func<IDockable?>>
        {
            ["LeftPane"] = () => _leftDock,
            ["BottomPane"] = () => _bottomDock,
            ["DocumentsPane"] = () => _documentDock
        };
        HostWindowLocator = new Dictionary<string, System.Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new Dock.Avalonia.Controls.HostWindow()
        };

        base.InitLayout(layout);
    }
}
