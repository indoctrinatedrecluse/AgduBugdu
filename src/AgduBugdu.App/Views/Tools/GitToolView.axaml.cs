using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Core.Git;

namespace AgduBugdu.App.Views.Tools;

public partial class GitToolView : UserControl
{
    public GitToolView()
    {
        InitializeComponent();
    }

    private void OnCommitKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            if (DataContext is GitToolViewModel vm)
            {
                _ = vm.CommitAsync();
                e.Handled = true;
            }
        }
    }

    private void OnFileItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: GitFileItem item } &&
            DataContext is GitToolViewModel vm)
        {
            _ = vm.ShowDiffAsync(item);
        }
    }
}
