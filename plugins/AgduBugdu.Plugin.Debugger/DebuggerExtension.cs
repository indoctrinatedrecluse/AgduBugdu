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

        // Populate default environment and process watch items
        _watches.Add(DebugProcessRunner.EvaluateExpression("OS", null, null));
        _watches.Add(DebugProcessRunner.EvaluateExpression(".NET", null, null));
        _watches.Add(DebugProcessRunner.EvaluateExpression("Processors", null, null));
        _watches.Add(DebugProcessRunner.EvaluateExpression("PID", null, null));
        _watches.Add(DebugProcessRunner.EvaluateExpression("Memory", null, null));

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
            DebugSessionState.Running => _runningProcess != null && !_runningProcess.HasExited
                ? $"Running (PID {_runningProcess.Id})"
                : "Running...",
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

        await _context.DebugService.StartDebuggingAsync(CurrentTarget);

        if (targetFile != null && File.Exists(targetFile))
        {
            _callStack.Add(new CallStackFrame("Main", targetFile, 1));

            // Check if there are active breakpoints
            var bpList = _context.DebugService.Breakpoints
                .Where(b => b.IsEnabled && string.Equals(b.FilePath, targetFile, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (bpList.Count > 0)
            {
                var bpLines = string.Join(", ", bpList.Select(b => b.Line));
                _context.DebugService.AppendOutput($"[Debugger] {bpList.Count} breakpoint(s) configured for {Path.GetFileName(targetFile)} (Lines: {bpLines})\n");
            }

            try
            {
                var (command, arguments, workingDir) = DebugProcessRunner.ResolveTargetCommand(targetFile, workspace);
                _context.DebugService.AppendOutput($"[Debugger] Launching: {command} {string.Join(" ", arguments)}\n");
                _context.DebugService.AppendOutput($"[Debugger] Working Directory: {workingDir}\n");

                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var arg in arguments)
                {
                    psi.ArgumentList.Add(arg);
                }

                _runningProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                var sw = Stopwatch.StartNew();

                _runningProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        HandleProcessOutput(e.Data);
                    }
                };

                _runningProcess.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        HandleProcessOutput(e.Data, isError: true);
                    }
                };

                _runningProcess.Exited += (s, e) =>
                {
                    sw.Stop();
                    int exitCode = -1;
                    try { exitCode = _runningProcess?.ExitCode ?? -1; } catch { }
                    _context.DebugService.AppendOutput($"\n[Debugger] Process exited with code {exitCode} ({sw.ElapsedMilliseconds}ms)\n");
                    RefreshWatches();
                    _ = _context.DebugService.StopDebuggingAsync();
                };

                if (_runningProcess.Start())
                {
                    _runningProcess.BeginOutputReadLine();
                    _runningProcess.BeginErrorReadLine();
                    _context.DebugService.AppendOutput($"[Debugger] Process started successfully with PID: {_runningProcess.Id}\n\n");
                    RefreshWatches();

                    // If a breakpoint is configured at line 1, pause immediately
                    var line1Bp = bpList.FirstOrDefault(b => b.Line == 1);
                    if (line1Bp != null)
                    {
                        await PauseAtLocationAsync(line1Bp.FilePath, line1Bp.Line);
                    }
                }
            }
            catch (Exception ex)
            {
                _context.DebugService.AppendOutput($"[Debugger] Process launch failed: {ex.Message}\n");
                RefreshWatches();
            }
        }
    }

    private void HandleProcessOutput(string line, bool isError = false)
    {
        _context.DebugService.AppendOutput(line + "\n");

        // Parse any runtime stack trace frames from exceptions/panics/tracebacks
        var frames = DebugProcessRunner.ParseStackTrace(line);
        if (frames.Count > 0)
        {
            foreach (var frame in frames)
            {
                if (!_callStack.Any(f => f.FilePath == frame.FilePath && f.LineNumber == frame.LineNumber))
                {
                    _callStack.Insert(0, frame);
                }
            }

            var primary = frames[0];
            _context.EditorService.OpenFile(primary.FilePath, primary.LineNumber);
            StateChanged?.Invoke(this, EventArgs.Empty);
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
        RefreshWatches();
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
        var evaluated = DebugProcessRunner.EvaluateExpression(expression.Trim(), _runningProcess, CurrentTarget);
        _watches.Add(evaluated);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshWatches()
    {
        for (int i = 0; i < _watches.Count; i++)
        {
            var old = _watches[i];
            var evaluated = DebugProcessRunner.EvaluateExpression(old.Name, _runningProcess, CurrentTarget);
            old.Value = evaluated.Value;
            old.Type = evaluated.Type;
        }
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
