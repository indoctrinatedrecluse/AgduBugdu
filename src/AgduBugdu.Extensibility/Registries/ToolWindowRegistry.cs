using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Extensibility.Registries;

public class ToolWindowRegistry : IToolWindowRegistry
{
    private readonly ConcurrentDictionary<string, ToolWindowDescriptor> _toolWindows = new();

    public void RegisterToolWindow(
        string id,
        string title,
        Func<object> viewModelFactory,
        Func<object, object> viewFactory,
        ToolDockPosition position = ToolDockPosition.Right)
    {
        _toolWindows[id] = new ToolWindowDescriptor(id, title, viewModelFactory, viewFactory, position);
    }

    public IReadOnlyDictionary<string, ToolWindowDescriptor> GetRegisteredTools() => _toolWindows;
}
