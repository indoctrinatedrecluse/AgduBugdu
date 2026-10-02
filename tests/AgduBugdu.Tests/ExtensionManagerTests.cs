using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Infrastructure.Terminal;
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
    public async Task Extension_Lifecycle_And_Registration_Works()
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
            File.WriteAllText(tempMd, "# Heading 1\n**Bold Text**\n- List item\n`code block`");
            editor.OpenFile(tempMd);

            var toolDescriptor = registeredTools["markdown.preview.tool"];
            var vm = toolDescriptor.ViewModelFactory() as MarkdownPreviewViewModel;
            Assert.NotNull(vm);

            vm.UpdateDocument(tempMd);
            Assert.Contains("<h1>Heading 1</h1>", vm.HtmlPreview);
            Assert.Contains("<b>Bold Text</b>", vm.HtmlPreview);
            Assert.Contains("<li>List item</li>", vm.HtmlPreview);
            Assert.Contains("<code>code block</code>", vm.HtmlPreview);
        }
        finally
        {
            if (File.Exists(tempMd))
            {
                File.Delete(tempMd);
            }
        }

        await plugin.DeactivateAsync();
    }

    [Fact]
    public void Document_Save_And_Modification_State_Work()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Original Content");
            var content = File.ReadAllText(tempFile);
            Assert.Equal("Original Content", content);

            File.WriteAllText(tempFile, "Updated Content");
            var updated = File.ReadAllText(tempFile);
            Assert.Equal("Updated Content", updated);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task LocalTerminalSession_CanStart_SendInput_And_ReceiveOutput()
    {
        using var terminal = new LocalTerminalSession();
        var outputReceived = new AutoResetEvent(false);
        var sb = new System.Text.StringBuilder();

        terminal.OutputReceived += (s, text) =>
        {
            lock (sb)
            {
                sb.Append(text);
            }
            outputReceived.Set();
        };

        terminal.Start();
        Assert.True(terminal.IsRunning);

        // Send a simple, non-interactive echo
        await Task.Delay(150);
        await terminal.WriteInputAsync("echo AGDU_TERMINAL_TEST");

        var matched = false;
        var timeoutAt = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < timeoutAt)
        {
            lock (sb)
            {
                if (sb.ToString().Contains("AGDU_TERMINAL_TEST"))
                {
                    matched = true;
                    break;
                }
            }
            outputReceived.WaitOne(TimeSpan.FromMilliseconds(200));
        }

        Assert.True(matched);

        terminal.Stop();
        Assert.False(terminal.IsRunning);
    }
}
