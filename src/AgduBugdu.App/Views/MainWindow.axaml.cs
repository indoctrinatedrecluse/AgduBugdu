using System;
using System.Collections.Generic;
using System.Windows.Input;
using AgduBugdu.App.ViewModels;
using AgduBugdu.App.Views.Tools;
using AgduBugdu.Infrastructure.Terminal;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AgduBugdu.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowPreviewKeyDown, RoutingStrategies.Tunnel);
        Closed += (s, e) => { if (DataContext is IDisposable d) d.Dispose(); };
        DataContextChanged += OnDataContextChanged;
    }

    private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var terminalTool = vm.TerminalTool;
        if (terminalTool == null) return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused == null) return;

        bool inTerminal = focused is TerminalToolView || focused.FindAncestorOfType<TerminalToolView>() != null;
        if (!inTerminal) return;

        var session = terminalTool.SelectedSession;
        if (session == null) return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // 1. In TUI mode, bypass ALL global window accelerators and dispatch directly to terminal
        if (terminalTool.IsTuiMode || session.IsTuiMode)
        {
            if (isCtrl && e.Key >= Key.A && e.Key <= Key.Z)
            {
                // Preserve copy if text is selected
                if (e.Key == Key.C && focused is TextBox tb && !string.IsNullOrEmpty(tb.SelectedText))
                {
                    return;
                }

                char c = (char)('A' + (e.Key - Key.A));
                var seq = TuiKeyTranslator.TranslateControlKey(c);
                if (seq != null)
                {
                    _ = session.SendRawInputAsync(seq);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Tab)
            {
                string seq = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "\x1b[Z" : "\t";
                _ = session.SendRawInputAsync(seq);
                e.Handled = true;
                return;
            }
        }
        else
        {
            // In standard mode, Ctrl+C sends interrupt when no text is selected
            if (isCtrl && e.Key == Key.C)
            {
                if (focused is TextBox tb && !string.IsNullOrEmpty(tb.SelectedText))
                {
                    return;
                }

                _ = session.SendRawInputAsync("\x03");
                e.Handled = true;
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.KeymapSynchronized += (s, ev) => ApplyDynamicKeyBindings(vm);
            ApplyDynamicKeyBindings(vm);
        }
    }

    private void ApplyDynamicKeyBindings(MainViewModel vm)
    {
        var map = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["file.new"] = vm.NewFileCommand,
            ["file.open"] = vm.OpenFileCommand,
            ["file.save"] = vm.SaveFileCommand,
            ["file.saveAs"] = vm.SaveFileAsCommand,
            ["file.closeTab"] = vm.CloseActiveTabCommand,
            ["file.reopenClosedTab"] = vm.ReopenClosedTabCommand,
            ["edit.find"] = vm.OpenFindCommand,
            ["edit.replace"] = vm.OpenReplaceCommand,
            ["edit.goToLine"] = vm.OpenGoToLineCommand,
            ["edit.goToMatchingBracket"] = vm.GoToMatchingBracketCommand,
            ["edit.toggleLineComment"] = vm.ToggleLineCommentCommand,
            ["edit.toggleBlockComment"] = vm.ToggleBlockCommentCommand,
            ["edit.moveLineUp"] = vm.MoveLineUpCommand,
            ["edit.moveLineDown"] = vm.MoveLineDownCommand,
            ["edit.duplicateLineUp"] = vm.DuplicateLineUpCommand,
            ["edit.duplicateLineDown"] = vm.DuplicateLineDownCommand,
            ["edit.deleteLine"] = vm.DeleteLineCommand,
            ["edit.joinLines"] = vm.JoinLinesCommand,
            ["edit.transformUppercase"] = vm.TransformUppercaseCommand,
            ["edit.transformLowercase"] = vm.TransformLowercaseCommand,
            ["view.toggleWordWrap"] = vm.ToggleWordWrapCommand,
            ["view.toggleMinimap"] = vm.ToggleMinimapCommand,
            ["view.toggleBottomPanel"] = vm.ToggleBottomPaneCommand,
            ["view.commandpalette"] = vm.OpenCommandPaletteCommand,
            ["view.settings"] = vm.OpenSettingsCommand,
            ["view.splitRight"] = vm.SplitEditorRightCommand,
            ["view.moveDocumentToOtherPane"] = vm.MoveDocumentToOtherPaneCommand,
            ["view.toggleExplorer"] = vm.FocusExplorerCommand,
            ["view.toggleSearch"] = vm.FocusSearchCommand,
            ["view.toggleGit"] = vm.FocusGitCommand,
            ["view.outline"] = vm.FocusOutlineCommand,
            ["view.toggleTerminal"] = vm.FocusTerminalCommand,
            ["view.toggleDebugger"] = vm.FocusDebuggerCommand,
            ["terminal.restart"] = vm.RestartTerminalCommand,
            ["debug.start"] = vm.StartOrContinueDebuggingCommand,
            ["debug.stepOver"] = vm.StepOverDebuggingCommand,
            ["debug.stepInto"] = vm.StepIntoDebuggingCommand,
            ["debug.stop"] = vm.StopDebuggingCommand
        };

        if (vm.SettingsModal?.AllCommands == null) return;

        // Clear and dynamically register key bindings
        KeyBindings.Clear();

        foreach (var cmd in vm.SettingsModal.AllCommands)
        {
            if (string.IsNullOrWhiteSpace(cmd.CurrentGesture))
                continue;

            if (map.TryGetValue(cmd.Id, out var command))
            {
                try
                {
                    var gesture = KeyGesture.Parse(cmd.CurrentGesture);
                    KeyBindings.Add(new KeyBinding
                    {
                        Gesture = gesture,
                        Command = command
                    });
                }
                catch
                {
                    // Fall back safely if gesture syntax cannot be parsed
                }
            }
        }
    }
}
