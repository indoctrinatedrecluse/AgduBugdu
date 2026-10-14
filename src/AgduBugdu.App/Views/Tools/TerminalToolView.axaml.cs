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

        var inputBox = this.FindControl<TextBox>("InputBox");
        if (inputBox != null)
        {
            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is TerminalToolViewModel vm)
                {
                    _ = vm.SendCommandAsync();
                    e.Handled = true;
                }
            };
        }

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
        OutputBox.Background = bg;
        OutputBox.Foreground = fg;
    }
}
