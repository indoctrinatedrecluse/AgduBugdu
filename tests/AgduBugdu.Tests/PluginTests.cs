using System.IO;
using System.Threading.Tasks;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Plugin.DataGridLive;
using AgduBugdu.Plugin.Debugger;
using AgduBugdu.Plugin.TodoExplorer;
using AgduBugdu.PluginContracts;
using Xunit;

namespace AgduBugdu.Tests;

public class PluginTests
{
    [Fact]
    public void CsvParser_Parses_Standard_And_Quoted_Fields()
    {
        var csv = "Name,Age,\"City, Country\"\nAlice,30,\"New York, USA\"\nBob,25,\"London, UK\"";
        var table = CsvParser.Parse(csv);

        Assert.Equal(3, table.Headers.Count);
        Assert.Equal("Name", table.Headers[0]);
        Assert.Equal("Age", table.Headers[1]);
        Assert.Equal("City, Country", table.Headers[2]);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Alice", table.Rows[0][0]);
        Assert.Equal("30", table.Rows[0][1]);
        Assert.Equal("New York, USA", table.Rows[0][2]);

        Assert.Equal("Bob", table.Rows[1][0]);
        Assert.Equal("London, UK", table.Rows[1][2]);
    }

    [Fact]
    public void CsvParser_AutoDetects_Tab_Delimiter()
    {
        var tsv = "ID\tProduct\tPrice\n101\tWidget\t19.99\n102\tGadget\t29.99";
        var table = CsvParser.Parse(tsv);

        Assert.Equal('\t', table.Delimiter);
        Assert.Equal(3, table.Headers.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Widget", table.Rows[0][1]);
    }

    [Fact]
    public void TodoScanner_Finds_And_Categorizes_Markers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var codeFile = Path.Combine(tempDir, "Sample.cs");
            File.WriteAllText(codeFile,
                "// TODO: Refactor this algorithm\n" +
                "public class Sample {\n" +
                "    // FIXME: Fix potential null reference\n" +
                "    /* NOTE: Cached for performance */\n" +
                "}\n"
            );

            var tasks = TodoScanner.ScanWorkspace(tempDir);
            Assert.Equal(3, tasks.Count);

            Assert.Contains(tasks, t => t.Tag == "TODO" && t.Message.Contains("Refactor this algorithm"));
            Assert.Contains(tasks, t => t.Tag == "FIXME" && t.Message.Contains("Fix potential null reference"));
            Assert.Contains(tasks, t => t.Tag == "NOTE" && t.Message.Contains("Cached for performance"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task Debugger_Manages_Breakpoints_And_Lifecycle()
    {
        var commands = new CommandRegistry();
        var tools = new ToolWindowRegistry();
        var editor = new MockEditorService();
        var workspace = new MockWorkspaceService();
        var context = new ExtensionContext(commands, tools, editor, workspace);

        var plugin = new DebuggerExtension();
        plugin.Initialize(context);
        await plugin.ActivateAsync();

        // 1. Breakpoint operations
        context.DebugService.ToggleBreakpoint("Test.cs", 10);
        Assert.True(context.DebugService.HasBreakpoint("Test.cs", 10));

        context.DebugService.ToggleBreakpoint("Test.cs", 10);
        Assert.False(context.DebugService.HasBreakpoint("Test.cs", 10));

        context.DebugService.SetBreakpoint("Test.cs", 25);
        Assert.Single(context.DebugService.Breakpoints);

        // 2. Lifecycle operations
        Assert.Equal(DebugSessionState.Idle, context.DebugService.State);

        await context.DebugService.StartDebuggingAsync("Test.cs");
        Assert.Equal(DebugSessionState.Running, context.DebugService.State);

        await context.DebugService.PauseAsync();
        Assert.Equal(DebugSessionState.Paused, context.DebugService.State);

        await context.DebugService.StepOverAsync();
        Assert.Equal(DebugSessionState.Paused, context.DebugService.State);

        await context.DebugService.ContinueAsync();
        Assert.Equal(DebugSessionState.Running, context.DebugService.State);

        await context.DebugService.StopDebuggingAsync();
        Assert.Equal(DebugSessionState.Idle, context.DebugService.State);

        context.DebugService.ClearAllBreakpoints();
        Assert.Empty(context.DebugService.Breakpoints);
    }
}
