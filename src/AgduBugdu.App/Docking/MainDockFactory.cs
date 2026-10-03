using System.Collections.Generic;
using System.Linq;
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
    private ProportionalDock? _mainLayout;
    private ProportionalDockSplitter? _bottomSplitter;
    private ProportionalDockSplitter? _leftDockSplitter;
    private ExplorerToolViewModel? _explorerTool;
    private OutputToolViewModel? _outputTool;
    private TerminalToolViewModel? _terminalTool;
    private TodoToolViewModel? _todoTool;
    private DebuggerToolViewModel? _debuggerTool;
    private bool _isBottomPaneMinimized;

    public IDocumentDock? DocumentDock => _documentDock;
    public ToolDock? BottomDock => _bottomDock;
    public ToolDock? LeftDock => _leftDock;
    public ExplorerToolViewModel? ExplorerTool => _explorerTool;
    public OutputToolViewModel? OutputTool => _outputTool;
    public TerminalToolViewModel? TerminalTool => _terminalTool;
    public TodoToolViewModel? TodoTool => _todoTool;
    public DebuggerToolViewModel? DebuggerTool => _debuggerTool;
    public bool IsBottomPaneMinimized => _isBottomPaneMinimized;

    public override IRootDock CreateLayout()
    {
        _explorerTool ??= new ExplorerToolViewModel();
        _outputTool ??= new OutputToolViewModel();
        _terminalTool ??= new TerminalToolViewModel();
        _todoTool ??= new TodoToolViewModel();
        _debuggerTool ??= new DebuggerToolViewModel();

        var welcomeDoc = new AvaloniaEdit.Document.TextDocument("Welcome to AgduBugdu Editor!\n\nPress Ctrl+P or Ctrl+Shift+P to open the Command Palette.\nUse File -> Open Folder... (Ctrl+K, Ctrl+O) to load a workspace.\nDouble-click any file in the Explorer to open it in a tab.\nDock panels (Explorer, Terminal, Output, TODO Tasks, Run & Debug) are draggable and re-arrangeable.\n");
        welcomeDoc.UndoStack.MarkAsOriginalFile();

        var doc1 = new EditorDocumentViewModel
        {
            FileName = "Welcome.txt",
            Title = "Welcome.txt",
            TextDocument = welcomeDoc,
            IsModified = false
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

        _bottomSplitter = new ProportionalDockSplitter();

        var centerLayout = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                documentDock,
                _bottomSplitter,
                bottomDock
            )
        };
        _centerLayout = centerLayout;

        _leftDockSplitter = new ProportionalDockSplitter();

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftDock,
                _leftDockSplitter,
                centerLayout
            )
        };
        _mainLayout = mainLayout;

        var rootDock = CreateRootDock();
        rootDock.IsCollapsable = false;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.ActiveDockable = mainLayout;

        _rootDock = rootDock;
        return rootDock;
    }

    public void RestoreLeftPane()
    {
        if (_leftDock == null || _explorerTool == null)
            return;

        if (_leftDock.VisibleDockables == null || !_leftDock.VisibleDockables.Contains(_explorerTool))
        {
            _leftDock.VisibleDockables ??= CreateList<IDockable>();
            _leftDock.VisibleDockables.Add(_explorerTool);
        }
        _leftDock.ActiveDockable = _explorerTool;

        if (_mainLayout?.VisibleDockables != null && _leftDockSplitter != null)
        {
            if (!_mainLayout.VisibleDockables.Contains(_leftDock))
            {
                _mainLayout.VisibleDockables.Insert(0, _leftDock);
            }
            if (!_mainLayout.VisibleDockables.Contains(_leftDockSplitter))
            {
                int idx = _mainLayout.VisibleDockables.IndexOf(_leftDock);
                _mainLayout.VisibleDockables.Insert(idx + 1, _leftDockSplitter);
            }
        }
        _leftDock.Proportion = 0.22;
    }

    public void MinimizeBottomPane()
    {
        if (_isBottomPaneMinimized || _centerLayout?.VisibleDockables == null || _bottomDock == null || _bottomSplitter == null)
            return;

        _centerLayout.VisibleDockables.Remove(_bottomSplitter);
        _centerLayout.VisibleDockables.Remove(_bottomDock);
        if (_documentDock != null)
        {
            _documentDock.Proportion = double.NaN;
        }
        _isBottomPaneMinimized = true;
    }

    public void RestoreBottomPane(IDockable? activeTool = null)
    {
        if (_bottomDock == null || _centerLayout?.VisibleDockables == null || _bottomSplitter == null)
            return;

        // Ensure bottom tools exist in VisibleDockables
        if (_bottomDock.VisibleDockables == null || _bottomDock.VisibleDockables.Count == 0)
        {
            _terminalTool ??= new TerminalToolViewModel();
            _outputTool ??= new OutputToolViewModel();
            _todoTool ??= new TodoToolViewModel();
            _debuggerTool ??= new DebuggerToolViewModel();
            _bottomDock.VisibleDockables = CreateList<IDockable>(_terminalTool, _outputTool, _todoTool, _debuggerTool);
        }
        else if (activeTool != null && !_bottomDock.VisibleDockables.Contains(activeTool))
        {
            _bottomDock.VisibleDockables.Add(activeTool);
        }

        if (_documentDock != null)
        {
            _documentDock.Proportion = 0.72;
        }
        _bottomDock.Proportion = 0.28;

        if (!_centerLayout.VisibleDockables.Contains(_bottomSplitter))
            _centerLayout.VisibleDockables.Add(_bottomSplitter);
        if (!_centerLayout.VisibleDockables.Contains(_bottomDock))
            _centerLayout.VisibleDockables.Add(_bottomDock);

        if (activeTool != null)
        {
            _bottomDock.ActiveDockable = activeTool;
        }
        else if (_bottomDock.ActiveDockable == null && _bottomDock.VisibleDockables.Count > 0)
        {
            _bottomDock.ActiveDockable = _bottomDock.VisibleDockables[0];
        }
        _isBottomPaneMinimized = false;
    }

    public void ToggleBottomPane()
    {
        if (_isBottomPaneMinimized)
            RestoreBottomPane();
        else
            MinimizeBottomPane();
    }

    public IRootDock ResetLayout()
    {
        var openDocs = _documentDock?.VisibleDockables != null 
            ? new List<IDockable>(_documentDock.VisibleDockables) 
            : null;
        var activeDoc = _documentDock?.ActiveDockable;
        var activeBottom = _bottomDock?.ActiveDockable;

        _isBottomPaneMinimized = false;

        var newRoot = CreateLayout();

        if (_documentDock != null && openDocs != null && openDocs.Count > 0)
        {
            _documentDock.VisibleDockables = CreateList(openDocs.ToArray());
            _documentDock.ActiveDockable = activeDoc ?? openDocs[0];
        }

        if (_bottomDock != null && activeBottom != null && _bottomDock.VisibleDockables != null)
        {
            var match = _bottomDock.VisibleDockables.FirstOrDefault(d => d.Id == activeBottom.Id);
            if (match != null)
            {
                _bottomDock.ActiveDockable = match;
            }
        }

        InitLayout(newRoot);
        return newRoot;
    }

    public void ResetPaneSizes()
    {
        ResetLayout();
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
            ["DocumentsPane"] = () => _documentDock,
            ["Explorer"] = () => _explorerTool,
            ["Output"] = () => _outputTool,
            ["Terminal"] = () => _terminalTool,
            ["TodoExplorer"] = () => _todoTool,
            ["Debugger"] = () => _debuggerTool
        };
        HostWindowLocator = new Dictionary<string, System.Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new Dock.Avalonia.Controls.HostWindow()
        };

        base.InitLayout(layout);
    }
}
