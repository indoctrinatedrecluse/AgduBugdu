using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Core.Text;

namespace AgduBugdu.App.Views.Tools;

public partial class SearchToolView : UserControl
{
    public SearchToolView()
    {
        InitializeComponent();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SearchToolViewModel vm)
        {
            _ = vm.SearchAsync();
            e.Handled = true;
        }
    }

    private void OnMatchDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: WorkspaceSearchMatch match } &&
            DataContext is SearchToolViewModel vm)
        {
            vm.MatchDoubleClicked(match);
        }
    }
}
