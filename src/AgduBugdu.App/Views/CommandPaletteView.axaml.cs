using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AgduBugdu.App.ViewModels;

namespace AgduBugdu.App.Views;

public partial class CommandPaletteView : UserControl
{
    public CommandPaletteView()
    {
        InitializeComponent();

        KeyDown += OnKeyDown;

        DataContextChanged += (s, e) =>
        {
            if (DataContext is CommandPaletteViewModel vm)
            {
                vm.PropertyChanged += (vs, pe) =>
                {
                    if (pe.PropertyName == nameof(CommandPaletteViewModel.IsOpen) && vm.IsOpen)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            SearchBox.Focus();
                            SearchBox.SelectAll();
                        }, DispatcherPriority.Input);
                    }
                };
            }
        };

        SearchBox.KeyDown += (s, e) =>
        {
            if (DataContext is CommandPaletteViewModel vm)
            {
                if (e.Key == Key.Down)
                {
                    vm.SelectNext();
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    vm.SelectPrevious();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    vm.ExecuteSelected();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.Close();
                    e.Handled = true;
                }
            }
        };
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
