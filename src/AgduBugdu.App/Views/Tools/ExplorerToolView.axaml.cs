using System;
using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.Models;
using AgduBugdu.App.ViewModels.Tools;

namespace AgduBugdu.App.Views.Tools;

public partial class ExplorerToolView : UserControl
{
    static ExplorerToolView()
    {
        TreeViewItem.IsExpandedProperty.Changed.Subscribe(e =>
        {
            if (e.Sender is TreeViewItem tvi &&
                e.NewValue.GetValueOrDefault() &&
                tvi.DataContext is FileSystemItem item &&
                item.IsDirectory)
            {
                if (!item.IsExpanded)
                {
                    item.IsExpanded = true;
                }
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    item.LoadChildren();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        });
    }

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
