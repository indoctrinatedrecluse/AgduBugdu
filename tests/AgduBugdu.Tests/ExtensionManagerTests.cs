using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Plugin.MarkdownLive;
using AgduBugdu.PluginContracts;
using Xunit;

namespace AgduBugdu.Tests;

public class MockWorkspaceService : IWorkspaceService
{
    public string? CurrentDirectory { get; private set; }
    public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    public void OpenFolder(string folderPath)
    {
        CurrentDirectory = folderPath;
        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(folderPath));
    }
}

public class MockEditorService : IEditorService
{
    public event EventHandler<DocumentEventArgs>? DocumentOpened;
    public event EventHandler<DocumentEventArgs>? DocumentSaved;
    public event EventHandler<DocumentEventArgs>? DocumentClosed;
    public event EventHandler<LineNavigationEventArgs>? LineNavigationRequested;

    public string? ActiveDocumentPath { get; private set; }
    public int? ActiveLine { get; set; } = 1;
    public int? ActiveColumn { get; set; } = 1;
    public string? DocumentContent { get; set; }

    public void OpenFile(string filePath)
    {
        OpenFile(filePath, 1, 1);
    }

    public void OpenFile(string filePath, int line, int column = 1)
    {
        ActiveDocumentPath = filePath;
        ActiveLine = line;
        ActiveColumn = column;
        DocumentOpened?.Invoke(this, new DocumentEventArgs(filePath));
        LineNavigationRequested?.Invoke(this, new LineNavigationEventArgs(filePath, line, column));
    }

    public void TriggerSave(string filePath)
    {
        DocumentSaved?.Invoke(this, new DocumentEventArgs(filePath));
    }

    public void TriggerClose(string filePath)
    {
        DocumentClosed?.Invoke(this, new DocumentEventArgs(filePath));
    }

    public void InsertText(string text)
    {
        DocumentContent = (DocumentContent ?? string.Empty) + text;
    }

    public string? GetActiveDocumentText() => DocumentContent;

    public void SetActiveDocumentText(string text)
    {
        DocumentContent = text;
    }

    public void NewDocument(string? defaultFileName = null, string? initialContent = null)
    {
        ActiveDocumentPath = defaultFileName;
        DocumentContent = initialContent ?? string.Empty;
        DocumentOpened?.Invoke(this, new DocumentEventArgs(defaultFileName ?? "Untitled.txt"));
    }
}

public class SampleTestExtension : IExtension
{
    public string Id => "test.sample";
    public string Name => "Sample Plugin";
    public string Version => "1.0.0";
    public bool Initialized { get; private set; }
    public bool Activated { get; private set; }
    public bool Deactivated { get; private set; }

    public void Initialize(IExtensionContext context)
    {
        Initialized = true;
        context.Commands.RegisterCommand("sample.hello", "Hello World", () => Task.CompletedTask);
        context.ToolWindows.RegisterToolWindow("sample.tool", "Sample Tool", () => new object(), vm => new object());
    }

    public Task ActivateAsync()
    {
        Activated = true;
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        Deactivated = true;
        return Task.CompletedTask;
    }
}

public class ExtensionManagerTests
{
    [Fact]
    public async Task Extension_Lifecycle_And_Registry_Integration()
    {
        var commands = new CommandRegistry();
        var tools = new ToolWindowRegistry();
        var editor = new MockEditorService();
        var workspace = new MockWorkspaceService();
        var context = new ExtensionContext(commands, tools, editor, workspace);

        var extension = new SampleTestExtension();
        extension.Initialize(context);
        await extension.ActivateAsync();

        Assert.True(extension.Initialized);
        Assert.True(extension.Activated);

        // Check command registered
        var registeredCommands = commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("sample.hello"));
        Assert.Equal("Hello World", registeredCommands["sample.hello"].Title);

        // Check tool window registered
        var registeredTools = tools.GetRegisteredTools();
        Assert.True(registeredTools.ContainsKey("sample.tool"));
        Assert.Equal("Sample Tool", registeredTools["sample.tool"].Title);

        await extension.DeactivateAsync();
        Assert.True(extension.Deactivated);
    }

    [Fact]
    public async Task MarkdownLive_Extension_Initializes_And_Registers_Endpoints()
    {
        var commands = new CommandRegistry();
        var tools = new ToolWindowRegistry();
        var editor = new MockEditorService();
        var workspace = new MockWorkspaceService();
        var context = new ExtensionContext(commands, tools, editor, workspace);

        var plugin = new MarkdownLiveExtension();
        Assert.Equal("agdubugdu.plugin.markdownlive", plugin.Id);
        Assert.Equal("Markdown Live Viewer", plugin.Name);

        plugin.Initialize(context);
        await plugin.ActivateAsync();

        // 1. Verify command registered in host command registry
        var registeredCommands = commands.GetRegisteredCommands();
        Assert.True(registeredCommands.ContainsKey("markdown.preview"));
        Assert.Equal("Markdown: Toggle Live Preview", registeredCommands["markdown.preview"].Title);

        // 2. Verify tool window registered in host tool registry
        var registeredTools = tools.GetRegisteredTools();
        Assert.True(registeredTools.ContainsKey("markdown.preview.tool"));
        Assert.Equal("Markdown Live Preview", registeredTools["markdown.preview.tool"].Title);

        // 3. Test Markdown Live Document conversion on document events
        var tempMd = Path.ChangeExtension(Path.GetTempFileName(), ".md");
        try
        {
            await File.WriteAllTextAsync(tempMd, "# Hello World\nTesting live preview.");

            editor.OpenFile(tempMd);

            var previewDoc = plugin.GetPreviewDocument(tempMd);
            Assert.NotNull(previewDoc);
            Assert.Contains("<h1>Hello World</h1>", previewDoc);
            Assert.Contains("<p>Testing live preview.</p>", previewDoc);
        }
        finally
        {
            if (File.Exists(tempMd))
                File.Delete(tempMd);
        }

        await plugin.DeactivateAsync();
    }
}
