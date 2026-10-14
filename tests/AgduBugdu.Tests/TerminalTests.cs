using System.Linq;
using AgduBugdu.App.ViewModels.Tools;
using Xunit;

namespace AgduBugdu.Tests;

public class TerminalTests
{
    [Fact]
    public void TerminalToolViewModel_InitializesWithDefaultSession()
    {
        var vm = new TerminalToolViewModel();

        Assert.Single(vm.Sessions);
        Assert.NotNull(vm.SelectedSession);
        Assert.Equal("PowerShell 1", vm.SelectedSession.Title);
        Assert.True(vm.SelectedSession.IsSelected);
        Assert.False(vm.IsSplitView);
        Assert.False(vm.IsSearchVisible);
    }

    [Fact]
    public void TerminalToolViewModel_AddMultipleShellSessions_TracksNamesAndActive()
    {
        var vm = new TerminalToolViewModel();

        vm.AddCommandPrompt();
        Assert.Equal(2, vm.Sessions.Count);
        Assert.Equal("CMD 1", vm.SelectedSession!.Title);
        Assert.True(vm.SelectedSession.IsSelected);
        Assert.False(vm.Sessions[0].IsSelected);

        vm.AddGitBash();
        Assert.Equal(3, vm.Sessions.Count);
        Assert.Equal("Git Bash 1", vm.SelectedSession!.Title);

        vm.AddWsl();
        Assert.Equal(4, vm.Sessions.Count);
        Assert.Equal("WSL 1", vm.SelectedSession!.Title);
    }

    [Fact]
    public void TerminalToolViewModel_SelectSession_UpdatesActiveState()
    {
        var vm = new TerminalToolViewModel();
        vm.AddPowerShell(); // PowerShell 2

        var first = vm.Sessions[0];
        var second = vm.Sessions[1];

        vm.SelectSession(first);
        Assert.Equal(first, vm.SelectedSession);
        Assert.True(first.IsSelected);
        Assert.False(second.IsSelected);

        vm.SelectSession(second);
        Assert.Equal(second, vm.SelectedSession);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    [Fact]
    public void TerminalToolViewModel_CloseSession_SelectsRemainingSession()
    {
        var vm = new TerminalToolViewModel();
        vm.AddCommandPrompt();

        var first = vm.Sessions[0];
        var second = vm.Sessions[1];

        vm.CloseSession(second);

        Assert.Single(vm.Sessions);
        Assert.Equal(first, vm.SelectedSession);
        Assert.True(first.IsSelected);
    }

    [Fact]
    public void TerminalToolViewModel_CloseAllSessions_RecreatesDefaultSession()
    {
        var vm = new TerminalToolViewModel();
        var initial = vm.Sessions[0];

        vm.CloseSession(initial);

        // Closes all -> auto-spawns a new session
        Assert.Single(vm.Sessions);
        Assert.NotNull(vm.SelectedSession);
        Assert.True(vm.SelectedSession.IsSelected);
    }

    [Fact]
    public void TerminalToolViewModel_ToggleSplitView_AssignsSplitSession()
    {
        var vm = new TerminalToolViewModel();
        vm.AddGitBash();

        vm.ToggleSplitView();
        Assert.True(vm.IsSplitView);
        Assert.NotNull(vm.SplitSession);
        Assert.NotEqual(vm.SelectedSession, vm.SplitSession);

        vm.ToggleSplitView();
        Assert.False(vm.IsSplitView);
        Assert.Null(vm.SplitSession);
    }

    [Fact]
    public void TerminalToolViewModel_SearchMatches_CountsOccurrences()
    {
        var vm = new TerminalToolViewModel();
        vm.TerminalOutput = "build succeeded\r\ntest succeeded\r\nall succeeded\r\n";

        vm.ToggleSearch();
        Assert.True(vm.IsSearchVisible);

        vm.SearchText = "succeeded";
        Assert.Equal(3, vm.SearchMatchCount);

        vm.SearchText = "fail";
        Assert.Equal(0, vm.SearchMatchCount);

        vm.ToggleSearch();
        Assert.False(vm.IsSearchVisible);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(0, vm.SearchMatchCount);
    }

    [Fact]
    public void TerminalToolViewModel_CommandDelegation_DispatchesToSelectedSession()
    {
        var vm = new TerminalToolViewModel();
        vm.CommandInput = "echo hello";
        Assert.Equal("echo hello", vm.SelectedSession!.CommandInput);

        vm.ClearTerminal();
        Assert.Empty(vm.TerminalOutput);
    }
}
