using System;
using AgduBugdu.Extensibility.Services;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Extensibility;

public class ExtensionContext : IExtensionContext
{
    public ICommandRegistry Commands { get; }
    public IToolWindowRegistry ToolWindows { get; }
    public IEditorService EditorService { get; }
    public IWorkspaceService WorkspaceService { get; }
    public IDebugService DebugService { get; }
    public ILanguageService Languages { get; }

    private readonly Action<string, string> _logger;

    public ExtensionContext(
        ICommandRegistry commands,
        IToolWindowRegistry toolWindows,
        IEditorService editorService,
        IWorkspaceService workspaceService,
        IDebugService? debugService = null,
        ILanguageService? languageService = null,
        Action<string, string>? logger = null)
    {
        Commands = commands;
        ToolWindows = toolWindows;
        EditorService = editorService;
        WorkspaceService = workspaceService;
        DebugService = debugService ?? new DefaultDebugService();
        Languages = languageService ?? new DefaultLanguageService();
        _logger = logger ?? ((msg, lvl) => Console.WriteLine($"[{lvl}] {msg}"));
    }

    public void Log(string message, string level = "Info")
    {
        _logger(message, level);
    }
}
