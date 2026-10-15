using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AgduBugdu.App.Themes;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Infrastructure.Terminal;

namespace AgduBugdu.App.Views.Tools;

public partial class TerminalToolView : UserControl
{
    public TerminalToolView()
    {
        InitializeComponent();

        void HookInputBox(string name, Func<TerminalToolViewModel, TerminalSessionItem?> getSession, Func<TerminalToolViewModel, Task> onEnter)
        {
            var box = this.FindControl<TextBox>(name);
            if (box != null)
            {
                box.KeyDown += (s, e) =>
                {
                    if (DataContext is not TerminalToolViewModel vm) return;

                    // Handle TUI or Interrupt keys first
                    if (HandleTuiOrInterruptKey(e, () => getSession(vm), isOutputBox: false, box))
                    {
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.Enter)
                    {
                        _ = onEnter(vm);
                        e.Handled = true;
                    }
                };
            }
        }

        HookInputBox("InputBox", vm => vm.SelectedSession, vm => vm.SendCommandAsync());
        HookInputBox("SplitInputBox1", vm => vm.SelectedSession, vm => vm.SendCommandAsync());
        HookInputBox("SplitInputBox2", vm => vm.SplitSession, vm => vm.SplitSession != null ? vm.SplitSession.SendCommandAsync() : Task.CompletedTask);

        if (OutputBox != null)
        {
            OutputBox.KeyDown += (s, e) =>
            {
                if (DataContext is TerminalToolViewModel vm)
                {
                    if (HandleTuiOrInterruptKey(e, () => vm.SelectedSession, isOutputBox: true, OutputBox))
                    {
                        e.Handled = true;
                    }
                }
            };

            OutputBox.TextInput += (s, e) =>
            {
                if (DataContext is TerminalToolViewModel vm && (vm.IsTuiMode || (vm.SelectedSession?.IsTuiMode ?? false)) && vm.SelectedSession != null && !string.IsNullOrEmpty(e.Text))
                {
                    _ = vm.SelectedSession.SendRawInputAsync(e.Text);
                    e.Handled = true;
                }
            };
        }

        ThemeManager.ThemeChanged += OnThemeChanged;
        ApplyTheme(ThemeManager.CurrentTheme);
    }

    private bool HandleTuiOrInterruptKey(KeyEventArgs e, Func<TerminalSessionItem?> getSession, bool isOutputBox, TextBox? textBox)
    {
        if (DataContext is not TerminalToolViewModel vm) return false;
        var session = getSession();
        if (session == null) return false;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // 1. Interrupt handling: Ctrl+C (Available in both standard and TUI mode)
        if (isCtrl && e.Key == Key.C)
        {
            // If user has highlighted text selected, preserve standard clipboard copy
            if (textBox != null && !string.IsNullOrEmpty(textBox.SelectedText))
            {
                return false;
            }

            _ = session.SendRawInputAsync("\x03");
            return true;
        }

        // If TUI mode is not active, let default text editing/navigation proceed
        if (!vm.IsTuiMode && !session.IsTuiMode)
        {
            return false;
        }

        // 2. Shift+Tab backtab handling
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = session.SendRawInputAsync("\x1b[Z");
            return true;
        }

        // 3. Generalized TUI control character handling: Ctrl+A through Ctrl+Z
        if (isCtrl)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                char c = (char)('A' + (e.Key - Key.A));
                var controlSeq = TuiKeyTranslator.TranslateControlKey(c);
                if (controlSeq != null)
                {
                    _ = session.SendRawInputAsync(controlSeq);
                    return true;
                }
            }
        }

        // 4. Generalized TUI navigation and editing sequences (VT100)
        var specialSeq = e.Key switch
        {
            Key.Up => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Up),
            Key.Down => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Down),
            Key.Left => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Left),
            Key.Right => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Right),
            Key.Home => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Home),
            Key.End => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.End),
            Key.PageUp => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.PageUp),
            Key.PageDown => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.PageDown),
            Key.Insert => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Insert),
            Key.Delete => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Delete),
            Key.Escape => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Escape),
            Key.Tab => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Tab),
            Key.Back => TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Backspace),
            Key.Enter or Key.Return => isOutputBox ? TuiKeyTranslator.TranslateSpecialKey(TuiSpecialKey.Enter) : null,
            Key.Space => isOutputBox ? " " : null,
            _ => null
        };

        if (specialSeq != null)
        {
            _ = session.SendRawInputAsync(specialSeq);
            return true;
        }

        return false;
    }

    private void OnThemeChanged(object? sender, ThemeDefinition theme)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyTheme(theme));
    }

    private void ApplyTheme(ThemeDefinition theme)
    {
        var bg = new SolidColorBrush(theme.TerminalBackground);
        var fg = new SolidColorBrush(theme.TerminalForeground);
        if (OutputBox != null)
        {
            OutputBox.Background = bg;
            OutputBox.Foreground = fg;
        }
    }
}
