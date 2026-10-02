using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.Debugger;

public class DebuggerViewModel
{
    private readonly IExtensionContext _context;
    private Process? _runningProcess;
    private StringBuilder _consoleOutput = new();
    private List<DebugWatchItem> _watches = new();
    private List<CallStackFrame> _callStack = new();

    public string Title => "Run & Debug";
    public string StatusText { get; private set; } = "Debugger Idle";
    public bool IsDebugging => _context.DebugService.State != DebugSessionState.Idle &&
                               _context.DebugService.State != DebugSessionState.Stopped;
    public bool IsPaused => _context.DebugService.State == DebugSessionState.Paused;

    public string? CurrentTarget { get; set; }
    public string DebugConsoleOutput => _consoleOutput.ToString();
    public IReadOnlyList<BreakpointInfo> Breakpoints => _context.DebugService.Breakpoints;
    public IReadOnlyList<DebugWatchItem> Watches => _watches;
    public IReadOnlyList<CallStackFrame> CallStack => _callStack;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? OutputUpdated;

    public DebuggerViewModel(IExtensionContext context)
    {
        _context = context;

        // Populate default environment watch items
        _watches.Add(new DebugWatchItem("OS", Environment.OSVersion.ToString(), "OperatingSystem"));
        _watches.Add(new DebugWatchItem(".NET", Environment.Version.ToString(), "Version"));
        _watches.Add(new DebugWatchItem("Processors", Environment.ProcessorCount.ToString(), "int"));

        _context.DebugService.BreakpointChanged += (s, e) =>
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        };

        _context.DebugService.StateChanged += (s, e) =>
        {
            UpdateStatus(e);
            StateChanged?.Invoke(this, EventArgs.Empty);
        };

        _context.DebugService.OutputReceived += (s, text) =>
        {
            _consoleOutput.Append(text);
            OutputUpdated?.Invoke(this, text);
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private void UpdateStatus(DebugStateChangedEventArgs e)
    {
        StatusText = e.NewState switch
        {
            DebugSessionState.Starting => "Starting debugger...",
            DebugSessionState.Running => "Running...",
            DebugSessionState.Paused => $"Paused at Ln {e.CurrentLine ?? 1} in {Path.GetFileName(e.CurrentFilePath ?? "file")}",
            DebugSessionState.Stopped => "Debugging stopped",
            _ => "Debugger Idle"
        };
    }

    public async Task StartOrContinueAsync()
    {
        if (IsPaused)
        {
            await _context.DebugService.ContinueAsync();
            return;
        }

        var targetFile = _context.EditorService.ActiveDocumentPath;
        var workspace = _context.WorkspaceService.CurrentDirectory;

        CurrentTarget = targetFile ?? workspace ?? "Host Application";
        _consoleOutput.Clear();
        _callStack.Clear();

        if (targetFile != null && File.Exists(targetFile))
        {
            _callStack.Add(new CallStackFrame("Main", targetFile, 1));
            _watches.Add(new DebugWatchItem("args", "string[0]", "string[]"));
            _watches.Add(new DebugWatchItem("activeFile", Path.GetFileName(targetFile), "string"));
        }

        await _context.DebugService.StartDebuggingAsync(CurrentTarget);

        // Check if there is a breakpoint at beginning or in active file
        if (targetFile != null)
        {
            var firstBp = _context.DebugService.Breakpoints.FirstOrDefault(b =>
                b.IsEnabled && string.Equals(b.FilePath, targetFile, StringComparison.OrdinalIgnoreCase));

            if (firstBp != null)
            {
                await Task.Delay(100);
                await PauseAtLocationAsync(firstBp.FilePath, firstBp.Line);
            }
        }
    }

    public async Task PauseAtLocationAsync(string file, int line)
    {
        _callStack.Clear();
        _callStack.Add(new CallStackFrame("Execute", file, line));

        _context.DebugService.AppendOutput($"[Debugger] Hit Breakpoint at {Path.GetFileName(file)}:{line}\n");
        _context.EditorService.OpenFile(file, line);
        await _context.DebugService.PauseAsync();
    }

    public async Task StepOverAsync()
    {
        if (!IsPaused) return;

        var file = _context.DebugService.CurrentExecutionFile;
        var line = (_context.DebugService.CurrentExecutionLine ?? 1) + 1;

        if (file != null)
        {
            _callStack.Clear();
            _callStack.Add(new CallStackFrame("Execute", file, line));
            _context.EditorService.OpenFile(file, line);
        }

        await _context.DebugService.StepOverAsync();
    }

    public async Task StepIntoAsync()
    {
        if (!IsPaused) return;

        var file = _context.DebugService.CurrentExecutionFile;
        var line = (_context.DebugService.CurrentExecutionLine ?? 1) + 1;

        if (file != null)
        {
            _callStack.Clear();
            _callStack.Add(new CallStackFrame("Execute", file, line));
            _context.EditorService.OpenFile(file, line);
        }

        await _context.DebugService.StepIntoAsync();
    }

    public async Task PauseAsync()
    {
        await _context.DebugService.PauseAsync();
        var file = _context.EditorService.ActiveDocumentPath;
        if (file != null)
        {
            _callStack.Clear();
            _callStack.Add(new CallStackFrame("Break", file, 1));
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_runningProcess != null && !_runningProcess.HasExited)
            {
                _runningProcess.Kill(true);
                _runningProcess.Dispose();
                _runningProcess = null;
            }
        }
        catch { }

        _callStack.Clear();
        await _context.DebugService.StopDebuggingAsync();
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await Task.Delay(100);
        await StartOrContinueAsync();
    }

    public void ToggleBreakpointCurrentLine()
    {
        var file = _context.EditorService.ActiveDocumentPath;
        var line = _context.EditorService.ActiveLine ?? 1;

        if (!string.IsNullOrEmpty(file))
        {
            _context.DebugService.ToggleBreakpoint(file, line);
        }
    }

    public void AddWatch(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return;
        _watches.Add(new DebugWatchItem(expression.Trim(), "(evaluated)", "dynamic"));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveWatch(DebugWatchItem item)
    {
        _watches.Remove(item);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearBreakpoints()
    {
        _context.DebugService.ClearAllBreakpoints();
    }

    public string ToFormattedSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Title} - [{StatusText}]");
        sb.AppendLine($"Target: {CurrentTarget ?? "(None)"}");
        sb.AppendLine(new string('=', 60));

        // Call stack
        sb.AppendLine("CALL STACK:");
        if (_callStack.Count == 0)
        {
            sb.AppendLine("  (Not running / No active stack frames)");
        }
        else
        {
            foreach (var frame in _callStack)
            {
                sb.AppendLine($"  -> {frame.DisplayText}");
            }
        }
        sb.AppendLine();

        // Variables
        sb.AppendLine("VARIABLES & WATCHES:");
        foreach (var w in _watches)
        {
            sb.AppendLine($"  {w.Name} ({w.Type}) = {w.Value}");
        }
        sb.AppendLine();

        // Breakpoints
        sb.AppendLine($"BREAKPOINTS ({Breakpoints.Count}):");
        if (Breakpoints.Count == 0)
        {
            sb.AppendLine("  (No breakpoints set. Press F9 in any file to add one)");
        }
        else
        {
            foreach (var bp in Breakpoints)
            {
                var flag = bp.IsEnabled ? "[x]" : "[ ]";
                sb.AppendLine($"  {flag} {Path.GetFileName(bp.FilePath)}:{bp.Line}");
            }
        }
        sb.AppendLine();

        // Console
        sb.AppendLine("DEBUG CONSOLE:");
        sb.AppendLine(_consoleOutput.Length > 0 ? _consoleOutput.ToString() : "  (No output yet)");

        return sb.ToString();
    }
}

public class DebuggerExtension : IExtension
{
    private IExtensionContext? _context;
    private DebuggerViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.debugger";
    public string Name => "Run & Debug Workbench";
    public string Version => "1.0.0";

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new DebuggerViewModel(context);

        // Register Command Palette Commands & Hotkeys
        _context.Commands.RegisterCommand(
            "debug.start",
            "Debug: Start / Continue (F5)",
            () => _viewModel.StartOrContinueAsync(),
            "F5"
        );

        _context.Commands.RegisterCommand(
            "debug.stepOver",
            "Debug: Step Over (F10)",
            () => _viewModel.StepOverAsync(),
            "F10"
        );

        _context.Commands.RegisterCommand(
            "debug.stepInto",
            "Debug: Step Into (F11)",
            () => _viewModel.StepIntoAsync(),
            "F11"
        );

        _context.Commands.RegisterCommand(
            "debug.pause",
            "Debug: Pause Execution",
            () => _viewModel.PauseAsync()
        );

        _context.Commands.RegisterCommand(
            "debug.stop",
            "Debug: Stop Debugging (Shift+F5)",
            () => _viewModel.StopAsync(),
            "Shift+F5"
        );

        _context.Commands.RegisterCommand(
            "debug.restart",
            "Debug: Restart Debugging",
            () => _viewModel.RestartAsync(),
            "Ctrl+Shift+F5"
        );

        _context.Commands.RegisterCommand(
            "debug.toggleBreakpoint",
            "Debug: Toggle Breakpoint on Current Line (F9)",
            () =>
            {
                _viewModel.ToggleBreakpointCurrentLine();
                return Task.CompletedTask;
            },
            "F9"
        );

        _context.Commands.RegisterCommand(
            "debug.clearBreakpoints",
            "Debug: Clear All Breakpoints",
            () =>
            {
                _viewModel.ClearBreakpoints();
                return Task.CompletedTask;
            }
        );

        // Register Tool Window
        _context.ToolWindows.RegisterToolWindow(
            "debug.workbench.tool",
            "Run & Debug",
            () => _viewModel,
            vm => _viewModel,
            ToolDockPosition.Left
        );

        _context.Log("Run & Debug Workbench extension initialized.");
    }

    public Task ActivateAsync() => Task.CompletedTask;
    public Task DeactivateAsync() => Task.CompletedTask;
}
