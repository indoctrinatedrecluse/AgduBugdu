using System;
using System.Collections.Generic;

namespace AgduBugdu.PluginContracts;

public enum ToolDockPosition
{
    Left,
    Right,
    Bottom
}

public record ToolWindowDescriptor(
    string Id,
    string Title,
    Func<object> ViewModelFactory,
    Func<object, object> ViewFactory,
    ToolDockPosition DefaultPosition
);

/// <summary>
/// Allows plugins to register dockable tool panels into the workspace.
/// </summary>
public interface IToolWindowRegistry
{
    void RegisterToolWindow(string id, string title, Func<object> viewModelFactory, Func<object, object> viewFactory, ToolDockPosition position = ToolDockPosition.Right);
    IReadOnlyDictionary<string, ToolWindowDescriptor> GetRegisteredTools();
}
