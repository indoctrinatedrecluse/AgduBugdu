using System.Linq;
using System.Text;
using Avalonia.Input;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.App.Views.Tools;
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

    [Fact]
    public void TuiKeyTranslator_ControlKeys_MapsAllControlCodesCorrectly()
    {
        Assert.Equal("\x03", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateControlKey('C'));
        Assert.Equal("\x18", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateControlKey('X'));
        Assert.Equal("\x0F", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateControlKey('O'));
        Assert.Equal("\x1A", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateControlKey('Z'));
        Assert.Equal("\x04", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateControlKey('D'));
        Assert.Equal("\x01", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateControlKey('a'));
    }

    [Fact]
    public void TuiKeyTranslator_SpecialKeys_MapsVt100Sequences()
    {
        Assert.Equal("\x1b[A", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Up));
        Assert.Equal("\x1b[B", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Down));
        Assert.Equal("\x1b[D", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Left));
        Assert.Equal("\x1b[C", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Right));
        Assert.Equal("\x1b", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Escape));
        Assert.Equal("\t", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Tab));
        Assert.Equal("\r", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.TranslateSpecialKey(AgduBugdu.Infrastructure.Terminal.TuiSpecialKey.Enter));
    }

    [Fact]
    public void TerminalToolViewModel_TuiModeToggle_UpdatesStateAndVisualProperties()
    {
        var vm = new TerminalToolViewModel();
        Assert.False(vm.IsTuiMode);
        Assert.Equal("🎮 TUI: OFF", vm.TuiModeButtonText);

        vm.ToggleTuiMode();
        Assert.True(vm.IsTuiMode);
        Assert.True(vm.SelectedSession!.IsTuiMode);
        Assert.Equal("🎮 TUI: ON", vm.TuiModeButtonText);

        vm.ToggleTuiMode();
        Assert.False(vm.IsTuiMode);
        Assert.Equal("🎮 TUI: OFF", vm.TuiModeButtonText);
    }

    [Fact]
    public void TerminalScreenBuffer_NormalTextAndCarriageReturn_HandlesOverwritesCorrectly()
    {
        var buffer = new AgduBugdu.Infrastructure.Terminal.TerminalScreenBuffer();
        buffer.Append("Downloading: [ 10% ]");
        Assert.Equal("Downloading: [ 10% ]", buffer.GetText());

        // Carriage return \r overwrites the current line
        buffer.Append("\rDownloading: [ 50% ]");
        Assert.Equal("Downloading: [ 50% ]", buffer.GetText());

        buffer.Append(Environment.NewLine + "Finished!");
        Assert.Contains("Downloading: [ 50% ]", buffer.GetText());
        Assert.Contains("Finished!", buffer.GetText());
    }

    [Fact]
    public void TerminalScreenBuffer_AlternateBuffer_EntersClearsAndRestoresNormalBuffer()
    {
        var buffer = new AgduBugdu.Infrastructure.Terminal.TerminalScreenBuffer();
        bool? tuiEventState = null;
        buffer.TuiStateChanged += (s, active) => tuiEventState = active;

        // Normal output
        buffer.Append("First Line" + Environment.NewLine + "Second Line");
        Assert.False(buffer.InAltBuffer);

        // Enter TUI alternate screen buffer (as emitted by nano, ir ntop, ir pmon, curses)
        buffer.Append("\x1b[?1049h\x1b[H\x1b[32m[CPU: 12% Mem: 45%]\x1b[0m\n[Tasks: 180]");
        Assert.True(buffer.InAltBuffer);
        Assert.True(tuiEventState);
        Assert.DoesNotContain("\x1b[32m", buffer.GetText());
        Assert.Contains("[CPU: 12% Mem: 45%]", buffer.GetText());
        Assert.Contains("[Tasks: 180]", buffer.GetText());
        Assert.DoesNotContain("First Line", buffer.GetText()); // Normal buffer was preserved

        // Next frame redraws in-place with clear screen \x1b[2J
        buffer.Append("\x1b[2J\x1b[H[CPU: 18% Mem: 46%]\n[Tasks: 182]");
        Assert.Contains("[CPU: 18% Mem: 46%]", buffer.GetText());
        Assert.DoesNotContain("12%", buffer.GetText()); // Previous frame cleared in-place

        // Exit TUI alternate screen buffer
        buffer.Append("\x1b[?1049l");
        Assert.False(buffer.InAltBuffer);
        Assert.False(tuiEventState);
        Assert.Contains("First Line", buffer.GetText()); // Normal scrollback restored!
        Assert.Contains("Second Line", buffer.GetText());
    }

    [Fact]
    public void TuiKeyTranslator_ResolveTuiAction_MapsAllKeysAndActions()
    {
        // Navigation
        Assert.Equal("\x1b[A", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("up"));
        Assert.Equal("\x1b[B", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("down"));
        Assert.Equal("\x1b[D", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("left"));
        Assert.Equal("\x1b[C", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("right"));
        Assert.Equal("\x1b[H", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("home"));
        Assert.Equal("\x1b[F", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("end"));
        Assert.Equal("\x1b[5~", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("pgup"));
        Assert.Equal("\x1b[6~", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("pgdn"));

        // WASD
        Assert.Equal("w", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("w"));
        Assert.Equal("a", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("a"));
        Assert.Equal("s", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("s"));
        Assert.Equal("d", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("d"));

        // App controls
        Assert.Equal("c", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("c"));
        Assert.Equal("m", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("m"));
        Assert.Equal("n", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("n"));
        Assert.Equal("p", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("p"));
        Assert.Equal("k", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("k"));
        Assert.Equal("y", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("y"));
        Assert.Equal("r", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("r"));
        Assert.Equal("q", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("q"));

        // Control chords
        Assert.Equal("\x03", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl-c"));
        Assert.Equal("\x18", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl+x"));
        Assert.Equal("\x0f", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl+o"));
        Assert.Equal("\x1a", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl-z"));
        Assert.Equal("\x04", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl+d"));
        Assert.Equal("\x13", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl+s"));
        Assert.Equal("\x17", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("ctrl+w"));

        // Action keys
        Assert.Equal("\r", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("enter"));
        Assert.Equal(" ", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("space"));
        Assert.Equal("\x1b", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("esc"));
        Assert.Equal("\t", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("tab"));
        Assert.Equal("\x7f", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("backspace"));
        Assert.Equal("\x1b[Z", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("shift+tab"));

        // Arbitrary single character passthrough
        Assert.Equal("5", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("5"));
        Assert.Equal("?", AgduBugdu.Infrastructure.Terminal.TuiKeyTranslator.ResolveTuiAction("?"));
    }

    [Fact]
    public void TerminalScreenBuffer_CursorPositioning_PlacesTextAtAbsolutePosition()
    {
        var buffer = new AgduBugdu.Infrastructure.Terminal.TerminalScreenBuffer();
        buffer.Append("\x1b[?1049h");
        buffer.Append("\x1b[3;5HABC");
        buffer.Append("\x1b[1;1Htop");

        Assert.Equal("top\n\n    ABC", buffer.GetText());
    }

    [Fact]
    public void TerminalScreenBuffer_EraseLineAndDisplay_RemovesContent()
    {
        var buffer = new AgduBugdu.Infrastructure.Terminal.TerminalScreenBuffer();
        buffer.Append("\x1b[?1049h");
        buffer.Append("hello world");
        buffer.Append("\r\x1b[K"); // erase from column 0 to end of line
        Assert.Equal(string.Empty, buffer.GetText());

        buffer.Append("abc");
        buffer.Append("\x1b[2K"); // erase whole line (cursor stays at column 3)
        Assert.Equal(string.Empty, buffer.GetText());

        buffer.Append("\x1b[1;1H"); // home
        buffer.Append("x");
        Assert.Equal("x", buffer.GetText());
    }

    [Fact]
    public void TerminalScreenBuffer_CursorMovementArrowCodes_OverwriteInPlace()
    {
        var buffer = new AgduBugdu.Infrastructure.Terminal.TerminalScreenBuffer();
        buffer.Append("\x1b[?1049h");
        buffer.Append("AB");
        buffer.Append("\x1b[D"); // left
        buffer.Append("X");     // overwrite 'B'
        Assert.Equal("AX", buffer.GetText());
    }

    [Fact]
    public void TerminalKeyMapper_ToRawSequence_MapsLettersDigitsCtrlAndSpecialKeys()
    {
        Assert.Equal("\x03", TerminalKeyMapper.ToRawSequence(Key.C, KeyModifiers.Control));
        Assert.Equal("\x18", TerminalKeyMapper.ToRawSequence(Key.X, KeyModifiers.Control));
        Assert.Equal("w", TerminalKeyMapper.ToRawSequence(Key.W, KeyModifiers.None));
        Assert.Equal("W", TerminalKeyMapper.ToRawSequence(Key.W, KeyModifiers.Shift));
        Assert.Equal("q", TerminalKeyMapper.ToRawSequence(Key.Q, KeyModifiers.None));
        Assert.Equal("5", TerminalKeyMapper.ToRawSequence(Key.D5, KeyModifiers.None));
        Assert.Equal("\x1b[A", TerminalKeyMapper.ToRawSequence(Key.Up, KeyModifiers.None));
        Assert.Equal("\r", TerminalKeyMapper.ToRawSequence(Key.Enter, KeyModifiers.None));
        Assert.Equal("\x1b", TerminalKeyMapper.ToRawSequence(Key.Escape, KeyModifiers.None));
        Assert.Equal(" ", TerminalKeyMapper.ToRawSequence(Key.Space, KeyModifiers.None));
    }

    [Fact]
    public async Task WindowsPseudoConsole_ExecutesCommand()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var pty = new AgduBugdu.Infrastructure.Terminal.WindowsPseudoConsole();
        var exited = new TaskCompletionSource<int>();
        pty.Exited += (s, c) => exited.TrySetResult(c);

        // Drain the output pipe in the background so the pseudo-console never blocks.
        _ = Task.Run(() =>
        {
            try
            {
                var buf = new byte[4096];
                while (pty.OutputStream.Read(buf, 0, buf.Length) > 0) { }
            }
            catch { /* pipe closed during teardown */ }
        });

        var markerFile = Path.Combine(Environment.CurrentDirectory, "conpty_test.txt");
        if (File.Exists(markerFile)) File.Delete(markerFile);

        try
        {
            pty.Start("cmd.exe /c \"echo CONPTY_OK > conpty_test.txt\"", Environment.CurrentDirectory, 120, 30);

            var winner = await Task.WhenAny(exited.Task, Task.Delay(15000));
            Assert.True(winner == exited.Task, "conpty process did not exit");
            Assert.Equal(0, await exited.Task);

            Assert.True(File.Exists(markerFile), "command did not execute inside the pseudo-console");
            Assert.Contains("CONPTY_OK", File.ReadAllText(markerFile));
        }
        finally
        {
            if (File.Exists(markerFile)) File.Delete(markerFile);
        }
    }
}
