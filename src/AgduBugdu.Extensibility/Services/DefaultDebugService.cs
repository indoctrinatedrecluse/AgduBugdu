using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Extensibility.Services;

public class DefaultDebugService : IDebugService
{
    private readonly ConcurrentDictionary<string, BreakpointInfo> _breakpoints = new();
    private DebugSessionState _state = DebugSessionState.Idle;
    private string? _currentExecutionFile;
    private int? _currentExecutionLine;

    public IReadOnlyList<BreakpointInfo> Breakpoints => _breakpoints.Values.OrderBy(b => b.FilePath).ThenBy(b => b.Line).ToList();
    public DebugSessionState State => _state;
    public string? CurrentExecutionFile => _currentExecutionFile;
    public int? CurrentExecutionLine => _currentExecutionLine;

    public event EventHandler<BreakpointChangedEventArgs>? BreakpointChanged;
    public event EventHandler<DebugStateChangedEventArgs>? StateChanged;
    public event EventHandler<string>? OutputReceived;

    public void ToggleBreakpoint(string filePath, int line)
    {
        var key = $"{filePath}:{line}";
        if (_breakpoints.TryRemove(key, out var existing))
        {
            BreakpointChanged?.Invoke(this, new BreakpointChangedEventArgs(existing, "Removed"));
        }
        else
        {
            var bp = new BreakpointInfo(filePath, line, true);
            _breakpoints[key] = bp;
            BreakpointChanged?.Invoke(this, new BreakpointChangedEventArgs(bp, "Added"));
        }
    }

    public void SetBreakpoint(string filePath, int line, bool enabled = true, string? condition = null)
    {
        var key = $"{filePath}:{line}";
        var bp = new BreakpointInfo(filePath, line, enabled, condition);
        _breakpoints[key] = bp;
        BreakpointChanged?.Invoke(this, new BreakpointChangedEventArgs(bp, "Updated"));
    }

    public void RemoveBreakpoint(string filePath, int line)
    {
        var key = $"{filePath}:{line}";
        if (_breakpoints.TryRemove(key, out var bp))
        {
            BreakpointChanged?.Invoke(this, new BreakpointChangedEventArgs(bp, "Removed"));
        }
    }

    public void ClearAllBreakpoints()
    {
        var existing = _breakpoints.Values.ToList();
        _breakpoints.Clear();
        foreach (var bp in existing)
        {
            BreakpointChanged?.Invoke(this, new BreakpointChangedEventArgs(bp, "Removed"));
        }
    }

    public bool HasBreakpoint(string filePath, int line)
    {
        var key = $"{filePath}:{line}";
        return _breakpoints.TryGetValue(key, out var bp) && bp.IsEnabled;
    }

    public void SetState(DebugSessionState newState, string? file = null, int? line = null, string? message = null)
    {
        var oldState = _state;
        _state = newState;
        _currentExecutionFile = file;
        _currentExecutionLine = line;
        StateChanged?.Invoke(this, new DebugStateChangedEventArgs(oldState, newState, file, line, message));
    }

    public void AppendOutput(string text)
    {
        OutputReceived?.Invoke(this, text);
    }

    public virtual Task StartDebuggingAsync(string? target = null)
    {
        SetState(DebugSessionState.Running, target, 1, "Debug session started");
        AppendOutput($"[Debugger] Started session for: {target ?? "active document"}\n");
        return Task.CompletedTask;
    }

    public virtual Task StepOverAsync()
    {
        if (_state == DebugSessionState.Paused && _currentExecutionLine.HasValue)
        {
            SetState(DebugSessionState.Paused, _currentExecutionFile, _currentExecutionLine.Value + 1, "Stepped over");
            AppendOutput($"[Debugger] Step Over -> line {_currentExecutionLine.Value}\n");
        }
        return Task.CompletedTask;
    }

    public virtual Task StepIntoAsync()
    {
        if (_state == DebugSessionState.Paused && _currentExecutionLine.HasValue)
        {
            SetState(DebugSessionState.Paused, _currentExecutionFile, _currentExecutionLine.Value + 1, "Stepped into");
            AppendOutput($"[Debugger] Step Into -> line {_currentExecutionLine.Value}\n");
        }
        return Task.CompletedTask;
    }

    public virtual Task ContinueAsync()
    {
        SetState(DebugSessionState.Running, _currentExecutionFile, null, "Continued execution");
        AppendOutput("[Debugger] Continued execution\n");
        return Task.CompletedTask;
    }

    public virtual Task PauseAsync()
    {
        SetState(DebugSessionState.Paused, _currentExecutionFile, _currentExecutionLine ?? 1, "Paused execution");
        AppendOutput("[Debugger] Paused by user\n");
        return Task.CompletedTask;
    }

    public virtual Task StopDebuggingAsync()
    {
        SetState(DebugSessionState.Stopped, null, null, "Debug session ended");
        AppendOutput("[Debugger] Debug session stopped\n");
        _state = DebugSessionState.Idle;
        return Task.CompletedTask;
    }
}
