using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Core.Docker;
using AgduBugdu.Extensibility;
using AgduBugdu.Extensibility.Registries;
using AgduBugdu.Plugin.Debugger;
using AgduBugdu.Tests;
using Xunit;

namespace AgduBugdu.Tests;

public class Phase3RealSystemsTests
{
    // ==========================================
    // 1. Docker Daemon Status & Explicit Demo Mode
    // ==========================================
    [Fact]
    public async Task TestDockerCliService_OfflineDoesNotSilentlyFakeContainers()
    {
        var service = new DockerCliService { IsDemoMode = false };

        // When demo mode is false and no local docker daemon is mocked/available:
        var containers = await service.GetContainersAsync();
        var images = await service.GetImagesAsync();

        // Must NOT silently enable DemoMode!
        Assert.False(service.IsDemoMode);

        // When offline, returns empty collection rather than faking 4 containers
        if (!await service.CheckDockerAvailableAsync())
        {
            Assert.Empty(containers);
            Assert.Empty(images);
        }
    }

    [Fact]
    public async Task TestDockerCliService_ExplicitDemoModeReturnsSimulatedContainers()
    {
        var service = new DockerCliService { IsDemoMode = true };

        var containers = await service.GetContainersAsync();
        var images = await service.GetImagesAsync();

        Assert.True(service.IsDemoMode);
        Assert.NotEmpty(containers);
        Assert.Contains(containers, c => c.Name == "agdubugdu-api");
        Assert.Contains(containers, c => c.Name == "postgres-db");
        Assert.NotEmpty(images);
    }

    [Fact]
    public async Task TestDockerToolViewModel_StateAndToggleDemoMode()
    {
        var vm = new DockerToolViewModel();
        await vm.RefreshAllAsync();

        // Initially demo mode is false unless Docker daemon is running
        if (!vm.IsDockerOnline)
        {
            Assert.True(vm.IsOfflineAndNotDemo);
            Assert.Contains("Offline", vm.DaemonStatus);
            Assert.Equal("#EF4444", vm.DaemonStatusColor);
        }

        // Toggle into Demo Mode
        await vm.ToggleDemoModeCommand.ExecuteAsync(null);
        Assert.True(vm.IsDemoMode);
        Assert.False(vm.IsOfflineAndNotDemo);
        Assert.Contains("Demo Mode", vm.DaemonStatus);
        Assert.Equal("#8B5CF6", vm.DaemonStatusColor);
        Assert.NotEmpty(vm.Containers);

        // Toggle back out of Demo Mode
        await vm.ToggleDemoModeCommand.ExecuteAsync(null);
        Assert.False(vm.IsDemoMode);
    }

    // ==========================================
    // 2. Debugger Command Resolution
    // ==========================================
    [Fact]
    public void TestDebugProcessRunner_ResolveTargetCommands()
    {
        // .cs file
        var (csCmd, csArgs, _) = DebugProcessRunner.ResolveTargetCommand("C:\\proj\\Program.cs");
        Assert.Equal("dotnet", csCmd);
        Assert.Contains("run", csArgs);

        // .csproj file
        var (csprojCmd, csprojArgs, _) = DebugProcessRunner.ResolveTargetCommand("C:\\proj\\App.csproj");
        Assert.Equal("dotnet", csprojCmd);
        Assert.Contains("--project", csprojArgs);

        // Python
        var (pyCmd, pyArgs, _) = DebugProcessRunner.ResolveTargetCommand("C:\\proj\\script.py");
        Assert.Equal("python", pyCmd);
        Assert.Contains("-u", pyArgs);

        // Node
        var (nodeCmd, nodeArgs, _) = DebugProcessRunner.ResolveTargetCommand("C:\\proj\\index.js");
        Assert.Equal("node", nodeCmd);
        Assert.Contains("C:\\proj\\index.js", nodeArgs);

        // PowerShell
        var (psCmd, psArgs, _) = DebugProcessRunner.ResolveTargetCommand("C:\\proj\\deploy.ps1");
        Assert.Equal("powershell", psCmd);
        Assert.Contains("-ExecutionPolicy", psArgs);
    }

    // ==========================================
    // 3. Debugger Stack Trace Parsing
    // ==========================================
    [Fact]
    public void TestDebugProcessRunner_ParseDotNetStackTrace()
    {
        string dotNetOutput = @"Unhandled exception. System.InvalidOperationException: Operation failed
   at AgduBugdu.Core.Engine.ExecuteStep(Int32 stepId) in C:\repos\AgduBugdu\Engine.cs:line 84
   at AgduBugdu.Core.Engine.Run() in C:\repos\AgduBugdu\Engine.cs:line 22
   at Program.Main(String[] args) in C:\repos\AgduBugdu\Program.cs:line 10";

        var frames = DebugProcessRunner.ParseStackTrace(dotNetOutput);

        Assert.Equal(3, frames.Count);
        Assert.Equal("AgduBugdu.Core.Engine.ExecuteStep", frames[0].FunctionName);
        Assert.Equal(@"C:\repos\AgduBugdu\Engine.cs", frames[0].FilePath);
        Assert.Equal(84, frames[0].LineNumber);

        Assert.Equal("Program.Main", frames[2].FunctionName);
        Assert.Equal(10, frames[2].LineNumber);
    }

    [Fact]
    public void TestDebugProcessRunner_ParsePythonTraceback()
    {
        string pythonOutput = @"Traceback (most recent call last):
  File ""C:\scripts\data_processor.py"", line 45, in process_batch
    result = compute_metrics(data)
  File ""C:\scripts\data_processor.py"", line 12, in compute_metrics
    raise ValueError('Negative variance detected')
ValueError: Negative variance detected";

        var frames = DebugProcessRunner.ParseStackTrace(pythonOutput);

        Assert.Equal(2, frames.Count);
        Assert.Equal("process_batch", frames[0].FunctionName);
        Assert.Equal(@"C:\scripts\data_processor.py", frames[0].FilePath);
        Assert.Equal(45, frames[0].LineNumber);

        Assert.Equal("compute_metrics", frames[1].FunctionName);
        Assert.Equal(12, frames[1].LineNumber);
    }

    [Fact]
    public void TestDebugProcessRunner_ParseNodeStackTrace()
    {
        string nodeOutput = @"TypeError: Cannot read properties of undefined (reading 'length')
    at processData (C:\app\server.js:52:19)
    at handleRequest (C:\app\server.js:18:5)";

        var frames = DebugProcessRunner.ParseStackTrace(nodeOutput);

        Assert.Equal(2, frames.Count);
        Assert.Equal("processData", frames[0].FunctionName);
        Assert.Equal(@"C:\app\server.js", frames[0].FilePath);
        Assert.Equal(52, frames[0].LineNumber);
    }

    // ==========================================
    // 4. Debugger Expression Evaluator
    // ==========================================
    [Fact]
    public void TestDebugProcessRunner_EvaluateExpressions()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Line 1\nLine 2\nLine 3\nLine 4");

            // 1. Math evaluation
            var mathAdd = DebugProcessRunner.EvaluateExpression("15 + 25", null, tempFile);
            Assert.Equal("40", mathAdd.Value);
            Assert.Equal("double", mathAdd.Type);

            var mathMul = DebugProcessRunner.EvaluateExpression("8 * 7", null, tempFile);
            Assert.Equal("56", mathMul.Value);

            // 2. File inspections
            var fileLines = DebugProcessRunner.EvaluateExpression("file.lines", null, tempFile);
            Assert.Equal("4", fileLines.Value);
            Assert.Equal("int", fileLines.Type);

            var fileName = DebugProcessRunner.EvaluateExpression("file.name", null, tempFile);
            Assert.Equal(Path.GetFileName(tempFile), fileName.Value);

            // 3. Process metrics without running process
            var pid = DebugProcessRunner.EvaluateExpression("PID", null, tempFile);
            Assert.Equal("N/A", pid.Value);

            // 4. Environment variable
            var envPath = DebugProcessRunner.EvaluateExpression("PATH", null, tempFile);
            Assert.False(string.IsNullOrEmpty(envPath.Value));
            Assert.Equal("string", envPath.Type);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public void TestDebuggerViewModel_WatchEvaluation()
    {
        var commands = new CommandRegistry();
        var tools = new ToolWindowRegistry();
        var editor = new MockEditorService();
        var workspace = new MockWorkspaceService();
        var context = new ExtensionContext(commands, tools, editor, workspace);

        var vm = new DebuggerViewModel(context);
        Assert.NotEmpty(vm.Watches);

        // Add custom watch that evaluates math
        vm.AddWatch("120 / 4");
        var watch = vm.Watches.FirstOrDefault(w => w.Name == "120 / 4");
        Assert.NotNull(watch);
        Assert.Equal("30", watch.Value);
        Assert.Equal("double", watch.Type);
    }
}
