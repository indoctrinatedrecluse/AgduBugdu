using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.ViewModels.Tools;

namespace AgduBugdu.App.Views.Tools;

public partial class TodoToolView : UserControl
{
    public TodoToolView()
    {
        InitializeComponent();

        DoubleTapped += (s, e) =>
        {
            if (DataContext is TodoToolViewModel vm && vm.SelectedItem != null)
            {
                vm.OpenSelectedTask(vm.SelectedItem);
                e.Handled = true;
            }
        };
    }
}
