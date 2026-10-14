using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AgduBugdu.App.Themes;
using AgduBugdu.App.ViewModels.Tools;

namespace AgduBugdu.App.Views.Tools;

public partial class TerminalToolView : UserControl
{
    public TerminalToolView()
    {
        InitializeComponent();

        void BindEnter(string name, Func<TerminalToolViewModel, Task> action)
        {
            var box = this.FindControl<TextBox>(name);
            if (box != null)
            {
                box.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter && DataContext is TerminalToolViewModel vm)
                    {
                        _ = action(vm);
                        e.Handled = true;
                    }
                };
            }
        }

        BindEnter("InputBox", vm => vm.SendCommandAsync());
        BindEnter("SplitInputBox1", vm => vm.SendCommandAsync());
        BindEnter("SplitInputBox2", vm => vm.SplitSession != null ? vm.SplitSession.SendCommandAsync() : Task.CompletedTask);

        ThemeManager.ThemeChanged += OnThemeChanged;
        ApplyTheme(ThemeManager.CurrentTheme);
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
