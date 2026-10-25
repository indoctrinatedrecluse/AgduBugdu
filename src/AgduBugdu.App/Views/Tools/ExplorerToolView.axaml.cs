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
                item.IsExpanded = true;
                item.LoadChildren();
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
