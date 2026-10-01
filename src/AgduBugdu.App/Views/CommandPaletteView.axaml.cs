using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.ViewModels;

namespace AgduBugdu.App.Views;

public partial class CommandPaletteView : UserControl
{
    public CommandPaletteView()
    {
        InitializeComponent();

        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm)
        {
            if (e.Key == Key.Escape)
            {
                vm.Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                vm.ExecuteSelected();
                e.Handled = true;
            }
        }
    }
}
