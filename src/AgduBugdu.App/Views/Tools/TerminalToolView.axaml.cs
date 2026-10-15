using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AgduBugdu.App.Themes;
using AgduBugdu.App.ViewModels.Tools;

namespace AgduBugdu.App.Views.Tools;

public partial class TerminalToolView : UserControl
{
    private TerminalToolViewModel? _vm;

    public TerminalToolView()
    {
        InitializeComponent();

        void HookKeySink(string name, Func<TerminalToolViewModel, TerminalSessionItem?> getSession, Func<TerminalToolViewModel, Task> onEnter)
        {
            var box = this.FindControl<TextBox>(name);
            if (box == null) return;

            box.KeyDown += (s, e) =>
            {
                if (DataContext is not TerminalToolViewModel vm) return;

                if (HandleKey(e, () => getSession(vm), box))
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

        // Output surfaces: the read-only boxes that capture TUI keystrokes.
        HookKeySink("OutputBox", vm => vm.SelectedSession, vm => Task.CompletedTask);
        HookKeySink("SplitOutputBox1", vm => vm.SelectedSession, vm => Task.CompletedTask);
        HookKeySink("SplitOutputBox2", vm => vm.SplitSession, vm => Task.CompletedTask);

        // Command input bars: normal command entry (hidden while TUI mode is active).
        HookKeySink("InputBox", vm => vm.SelectedSession, vm => vm.SendCommandAsync());
        HookKeySink("SplitInputBox1", vm => vm.SelectedSession, vm => vm.SendCommandAsync());
        HookKeySink("SplitInputBox2", vm => vm.SplitSession, vm => vm.SplitSession != null ? vm.SplitSession.SendCommandAsync() : Task.CompletedTask);

        DataContextChanged += OnDataContextChanged;
        ThemeManager.ThemeChanged += OnThemeChanged;
        ApplyTheme(ThemeManager.CurrentTheme);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is TerminalToolViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
            if (vm.IsTuiMode) FocusOutputBox();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalToolViewModel.IsTuiMode) && _vm?.IsTuiMode == true)
        {
            FocusOutputBox();
        }
    }

    private void FocusOutputBox()
    {
        var box = OutputBox ?? this.FindControl<TextBox>("OutputBox");
        Avalonia.Threading.Dispatcher.UIThread.Post(() => box?.Focus());
    }

    /// <summary>
    /// Routes a pressed key to the shell. Outside TUI mode only Ctrl+C (SIGINT) is
    /// intercepted so the TextBox can handle normal editing and command entry. Inside
    /// TUI mode every recognized key maps directly to raw terminal input.
    /// </summary>
    private bool HandleKey(KeyEventArgs e, Func<TerminalSessionItem?> getSession, TextBox textBox)
    {
        if (DataContext is not TerminalToolViewModel vm) return false;
        var session = getSession();
        if (session == null) return false;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isTui = vm.IsTuiMode || session.IsTuiMode;

        // Ctrl+C always sends SIGINT (both normal and TUI modes), unless the user is
        // copying highlighted terminal text.
        if (isCtrl && e.Key == Key.C)
        {
            if (!string.IsNullOrEmpty(textBox.SelectedText)) return false;
            _ = session.SendRawInputAsync("\x03");
            return true;
        }

        // Outside TUI mode only the interrupt above is special.
        if (!isTui) return false;

        // In TUI mode every recognized key maps directly to raw terminal input.
        var seq = TerminalKeyMapper.ToRawSequence(e.Key, e.KeyModifiers);
        if (seq != null)
        {
            _ = session.SendRawInputAsync(seq);
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
