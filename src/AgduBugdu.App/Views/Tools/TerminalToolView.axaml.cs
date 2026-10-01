using Avalonia.Controls;
using Avalonia.Input;
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
    }
}
