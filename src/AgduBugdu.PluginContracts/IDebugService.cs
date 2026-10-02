using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgduBugdu.PluginContracts;

public enum DebugSessionState
{
    Idle,
    Starting,
    Running,
    Paused,
    Stopped
}

public record BreakpointInfo(string FilePath, int Line, bool IsEnabled = true, string? Condition = null);

public class BreakpointChangedEventArgs : EventArgs
{
    public BreakpointInfo Breakpoint { get; }
    public string Action { get; } // "Added", "Removed", "Updated"

    public BreakpointChangedEventArgs(BreakpointInfo breakpoint, string action)
    {
        Breakpoint = breakpoint;
        Action = action;
    }
}

public class DebugStateChangedEventArgs : EventArgs
{
    public DebugSessionState OldState { get; }
    public DebugSessionState NewState { get; }
    public string? CurrentFilePath { get; }
    public int? CurrentLine { get; }
    public string? Message { get; }

    public DebugStateChangedEventArgs(
        DebugSessionState oldState,
        DebugSessionState newState,
        string? currentFilePath = null,
        int? currentLine = null,
        string? message = null)
    {
        OldState = oldState;
        NewState = newState;
        CurrentFilePath = currentFilePath;
        CurrentLine = currentLine;
        Message = message;
    }
}

public interface IDebugService
{
    IReadOnlyList<BreakpointInfo> Breakpoints { get; }
    DebugSessionState State { get; }
    string? CurrentExecutionFile { get; }
    int? CurrentExecutionLine { get; }

    event EventHandler<BreakpointChangedEventArgs>? BreakpointChanged;
    event EventHandler<DebugStateChangedEventArgs>? StateChanged;
    event EventHandler<string>? OutputReceived;

    void ToggleBreakpoint(string filePath, int line);
    void SetBreakpoint(string filePath, int line, bool enabled = true, string? condition = null);
    void RemoveBreakpoint(string filePath, int line);
    void ClearAllBreakpoints();
    bool HasBreakpoint(string filePath, int line);

    Task StartDebuggingAsync(string? target = null);
    Task StepOverAsync();
    Task StepIntoAsync();
    Task ContinueAsync();
    Task PauseAsync();
    Task StopDebuggingAsync();
    void AppendOutput(string text);
}
