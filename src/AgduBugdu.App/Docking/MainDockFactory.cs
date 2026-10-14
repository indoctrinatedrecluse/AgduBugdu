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
    private IDocumentDock? _secondaryDocumentDock;
    private ProportionalDock? _editorLayout;
    private ProportionalDockSplitter? _secondarySplitter;
    private ToolDock? _bottomDock;
    private ToolDock? _leftDock;
    private ProportionalDock? _centerLayout;
    private ProportionalDock? _mainLayout;
    private ProportionalDockSplitter? _bottomSplitter;
    private ProportionalDockSplitter? _leftDockSplitter;
    private ExplorerToolViewModel? _explorerTool;
    private SearchToolViewModel? _searchTool;
    private GitToolViewModel? _gitTool;
    private OutlineToolViewModel? _outlineTool;
    private OutputToolViewModel? _outputTool;
    private TerminalToolViewModel? _terminalTool;
    private TodoToolViewModel? _todoTool;
    private DebuggerToolViewModel? _debuggerTool;
    private NetworkSuiteToolViewModel? _networkSuiteTool;
    private DockerToolViewModel? _dockerTool;
    private bool _isBottomPaneMinimized;

    public IDocumentDock? DocumentDock => _documentDock;
    public IDocumentDock? SecondaryDocumentDock => _secondaryDocumentDock;
    public bool IsSplitActive => _secondaryDocumentDock != null && _editorLayout?.VisibleDockables?.Contains(_secondaryDocumentDock) == true;
    public ProportionalDock? EditorLayout => _editorLayout;
    public ToolDock? BottomDock => _bottomDock;
    public ToolDock? LeftDock => _leftDock;
    public ExplorerToolViewModel? ExplorerTool => _explorerTool;
    public SearchToolViewModel? SearchTool => _searchTool;
    public GitToolViewModel? GitTool => _gitTool;
    public OutlineToolViewModel? OutlineTool => _outlineTool;
    public OutputToolViewModel? OutputTool => _outputTool;
    public TerminalToolViewModel? TerminalTool => _terminalTool;
    public TodoToolViewModel? TodoTool => _todoTool;
    public DebuggerToolViewModel? DebuggerTool => _debuggerTool;
    public NetworkSuiteToolViewModel? NetworkSuiteTool => _networkSuiteTool;
    public DockerToolViewModel? DockerTool => _dockerTool;
    public bool IsBottomPaneMinimized => _isBottomPaneMinimized;
    public IDockable? CurrentActiveDockable { get; private set; }
    public IDockable? ActiveDockable => CurrentActiveDockable ?? _secondaryDocumentDock?.ActiveDockable ?? _documentDock?.ActiveDockable;

    public override void SetActiveDockable(IDockable dockable)
    {
        CurrentActiveDockable = dockable;
        base.SetActiveDockable(dockable);
    }

    public override IRootDock CreateLayout()
    {
        _explorerTool ??= new ExplorerToolViewModel();
        _searchTool ??= new SearchToolViewModel();
        _gitTool ??= new GitToolViewModel();
        _outlineTool ??= new OutlineToolViewModel();
        _outputTool ??= new OutputToolViewModel();
        _terminalTool ??= new TerminalToolViewModel();
        _todoTool ??= new TodoToolViewModel();
        _debuggerTool ??= new DebuggerToolViewModel();
        _networkSuiteTool ??= new NetworkSuiteToolViewModel();
        _dockerTool ??= new DockerToolViewModel();

        var welcomeDoc = new AvaloniaEdit.Document.TextDocument("Welcome to AgduBugdu Editor!\n\nPress Ctrl+P or Ctrl+Shift+P to open the Command Palette.\nUse File -> Open Folder... (Ctrl+K, Ctrl+O) to load a workspace.\nDouble-click any file in the Explorer to open it in a tab.\nDock panels (Explorer, Terminal, Output, TODO Tasks, Run & Debug, Outline) are draggable and re-arrangeable.\n");
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
            VisibleDockables = CreateList<IDockable>(_explorerTool, _searchTool, _gitTool, _outlineTool),
            ActiveDockable = _explorerTool
        };
        _leftDock = leftDock;

        var bottomDock = new ToolDock
        {
            Id = "BottomPane",
            Title = "Panel",
            Proportion = 0.28,
            VisibleDockables = CreateList<IDockable>(_terminalTool, _outputTool, _todoTool, _debuggerTool, _networkSuiteTool, _dockerTool),
            ActiveDockable = _terminalTool
        };
        _bottomDock = bottomDock;

        var documentDock = new DocumentDock
        {
            Id = "DocumentsPane",
            Title = "Documents",
            Proportion = 1.0,
            VisibleDockables = CreateList<IDockable>(doc1),
            ActiveDockable = doc1,
            CanCreateDocument = true
        };
        _documentDock = documentDock;

        var editorLayout = new ProportionalDock
        {
            Id = "EditorLayout",
            Orientation = Orientation.Horizontal,
            Proportion = 0.72,
            VisibleDockables = CreateList<IDockable>(documentDock)
        };
        _editorLayout = editorLayout;

        _bottomSplitter = new ProportionalDockSplitter();

        var centerLayout = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                editorLayout,
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

    public void RestoreLeftPane(IDockable? activeTool = null)
    {
        if (_leftDock == null)
            return;

        _explorerTool ??= new ExplorerToolViewModel();
        _searchTool ??= new SearchToolViewModel();
        _gitTool ??= new GitToolViewModel();
        _outlineTool ??= new OutlineToolViewModel();

        _leftDock.VisibleDockables ??= CreateList<IDockable>();
        if (!_leftDock.VisibleDockables.Contains(_explorerTool))
            _leftDock.VisibleDockables.Add(_explorerTool);
        if (!_leftDock.VisibleDockables.Contains(_searchTool))
            _leftDock.VisibleDockables.Add(_searchTool);
        if (!_leftDock.VisibleDockables.Contains(_gitTool))
            _leftDock.VisibleDockables.Add(_gitTool);
        if (!_leftDock.VisibleDockables.Contains(_outlineTool))
            _leftDock.VisibleDockables.Add(_outlineTool);

        _leftDock.ActiveDockable = activeTool ?? _explorerTool;

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

    public void SplitEditor(Orientation orientation = Orientation.Horizontal)
    {
        if (_editorLayout == null || _documentDock == null)
            return;

        _editorLayout.Orientation = orientation;

        if (_secondaryDocumentDock == null)
        {
            _secondaryDocumentDock = new DocumentDock
            {
                Id = "SecondaryDocumentsPane",
                Title = "Documents (Split)",
                Proportion = 0.5,
                CanCreateDocument = true
            };
            _secondarySplitter = new ProportionalDockSplitter();
        }

        _documentDock.Proportion = 0.5;
        _secondaryDocumentDock.Proportion = 0.5;

        _editorLayout.VisibleDockables ??= CreateList<IDockable>();
        if (_secondarySplitter != null && !_editorLayout.VisibleDockables.Contains(_secondarySplitter))
            _editorLayout.VisibleDockables.Add(_secondarySplitter);
        if (!_editorLayout.VisibleDockables.Contains(_secondaryDocumentDock))
            _editorLayout.VisibleDockables.Add(_secondaryDocumentDock);

        // If secondary dock has no documents, clone active document or create welcome
        if (_secondaryDocumentDock.VisibleDockables == null || _secondaryDocumentDock.VisibleDockables.Count == 0)
        {
            if (_documentDock.ActiveDockable is EditorDocumentViewModel activeDoc && !string.IsNullOrEmpty(activeDoc.FilePath))
            {
                var clone = EditorDocumentViewModel.FromFile(activeDoc.FilePath);
                _secondaryDocumentDock.VisibleDockables = CreateList<IDockable>(clone);
                _secondaryDocumentDock.ActiveDockable = clone;
            }
        }
    }

    public void CloseSplit()
    {
        if (_editorLayout == null || _secondaryDocumentDock == null || _secondarySplitter == null)
            return;

        // Move all documents in secondary dock back to primary dock
        if (_secondaryDocumentDock.VisibleDockables != null && _documentDock?.VisibleDockables != null)
        {
            var docsToMove = _secondaryDocumentDock.VisibleDockables.ToList();
            foreach (var doc in docsToMove)
            {
                _secondaryDocumentDock.VisibleDockables.Remove(doc);
                if (!_documentDock.VisibleDockables.Contains(doc))
                {
                    _documentDock.VisibleDockables.Add(doc);
                }
            }
        }

        if (_editorLayout.VisibleDockables != null)
        {
            _editorLayout.VisibleDockables.Remove(_secondarySplitter);
            _editorLayout.VisibleDockables.Remove(_secondaryDocumentDock);
        }
        if (_documentDock != null)
        {
            _documentDock.Proportion = 1.0;
        }
    }

    public void ToggleSplitEditor()
    {
        if (IsSplitActive)
            CloseSplit();
        else
            SplitEditor(Orientation.Horizontal);
    }

    public void MoveActiveDocumentToOtherPane()
    {
        if (!IsSplitActive)
        {
            SplitEditor(Orientation.Horizontal);
        }

        if (_documentDock == null || _secondaryDocumentDock == null)
            return;

        // If secondary dock is active or contains the current active dockable
        bool isSecondaryActive = _secondaryDocumentDock.ActiveDockable != null && 
            (ActiveDockable == _secondaryDocumentDock.ActiveDockable || _documentDock.ActiveDockable == null);

        if (isSecondaryActive && _secondaryDocumentDock.ActiveDockable != null)
        {
            var doc = _secondaryDocumentDock.ActiveDockable;
            _secondaryDocumentDock.VisibleDockables?.Remove(doc);
            _documentDock.VisibleDockables ??= CreateList<IDockable>();
            if (!_documentDock.VisibleDockables.Contains(doc))
            {
                _documentDock.VisibleDockables.Add(doc);
            }
            _documentDock.ActiveDockable = doc;
            SetActiveDockable(doc);
        }
        else if (_documentDock.ActiveDockable != null)
        {
            var doc = _documentDock.ActiveDockable;
            _documentDock.VisibleDockables?.Remove(doc);
            _secondaryDocumentDock.VisibleDockables ??= CreateList<IDockable>();
            if (!_secondaryDocumentDock.VisibleDockables.Contains(doc))
            {
                _secondaryDocumentDock.VisibleDockables.Add(doc);
            }
            _secondaryDocumentDock.ActiveDockable = doc;
            SetActiveDockable(doc);
        }
    }

    public void MinimizeBottomPane()
    {
        if (_isBottomPaneMinimized || _centerLayout?.VisibleDockables == null || _bottomDock == null || _bottomSplitter == null)
            return;

        _centerLayout.VisibleDockables.Remove(_bottomSplitter);
        _centerLayout.VisibleDockables.Remove(_bottomDock);
        if (_editorLayout != null)
        {
            _editorLayout.Proportion = double.NaN;
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

        if (_editorLayout != null)
        {
            _editorLayout.Proportion = 0.72;
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
        var activeLeft = _leftDock?.ActiveDockable;

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

        if (_leftDock != null && activeLeft != null && _leftDock.VisibleDockables != null)
        {
            var match = _leftDock.VisibleDockables.FirstOrDefault(d => d.Id == activeLeft.Id);
            if (match != null)
            {
                _leftDock.ActiveDockable = match;
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
            ["Search"] = () => _searchTool,
            ["SourceControl"] = () => _gitTool,
            ["Outline"] = () => _outlineTool,
            ["Output"] = () => _outputTool,
            ["Terminal"] = () => _terminalTool,
            ["TodoExplorer"] = () => _todoTool,
            ["Debugger"] = () => _debuggerTool,
            ["NetworkSuite"] = () => _networkSuiteTool,
            ["DockerWorkbench"] = () => _dockerTool
        };
        DockableLocator = new Dictionary<string, System.Func<IDockable?>>
        {
            ["LeftPane"] = () => _leftDock,
            ["BottomPane"] = () => _bottomDock,
            ["DocumentsPane"] = () => _documentDock,
            ["SecondaryDocumentsPane"] = () => _secondaryDocumentDock,
            ["Explorer"] = () => _explorerTool,
            ["Search"] = () => _searchTool,
            ["SourceControl"] = () => _gitTool,
            ["Outline"] = () => _outlineTool,
            ["Output"] = () => _outputTool,
            ["Terminal"] = () => _terminalTool,
            ["TodoExplorer"] = () => _todoTool,
            ["Debugger"] = () => _debuggerTool,
            ["NetworkSuite"] = () => _networkSuiteTool,
            ["DockerWorkbench"] = () => _dockerTool
        };
        HostWindowLocator = new Dictionary<string, System.Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new Dock.Avalonia.Controls.HostWindow()
        };

        base.InitLayout(layout);
    }
}
