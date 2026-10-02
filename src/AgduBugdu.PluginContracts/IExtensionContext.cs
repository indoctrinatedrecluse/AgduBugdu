namespace AgduBugdu.PluginContracts;

/// <summary>
/// Execution context and services exposed by the host application to extensions.
/// </summary>
public interface IExtensionContext
{
    /// <summary>
    /// Command and menu contribution registry.
    /// </summary>
    ICommandRegistry Commands { get; }

    /// <summary>
    /// Dockable tool panel registry.
    /// </summary>
    IToolWindowRegistry ToolWindows { get; }

    /// <summary>
    /// Editor buffer hooks and events.
    /// </summary>
    IEditorService EditorService { get; }

    /// <summary>
    /// Workspace and file system information.
    /// </summary>
    IWorkspaceService WorkspaceService { get; }

    /// <summary>
    /// Debugging and breakpoints subsystem.
    /// </summary>
    IDebugService DebugService { get; }

    /// <summary>
    /// Log messages to the editor's output window and log files.
    /// </summary>
    void Log(string message, string level = "Info");
}
