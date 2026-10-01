using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.Models;
using AgduBugdu.App.ViewModels.Tools;

namespace AgduBugdu.App.Views.Tools;

public partial class ExplorerToolView : UserControl
{
    public ExplorerToolView()
    {
        InitializeComponent();

        DoubleTapped += OnDoubleTapped;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ExplorerToolViewModel vm && vm.SelectedItem != null)
        {
            vm.ItemDoubleClicked(vm.SelectedItem);
            e.Handled = true;
        }
    }
}
