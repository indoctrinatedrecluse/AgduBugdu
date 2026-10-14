using Avalonia.Controls;
using Avalonia.Input;
using AgduBugdu.App.ViewModels.Tools;
using AgduBugdu.Core.Symbols;

namespace AgduBugdu.App.Views.Tools;

public partial class OutlineToolView : UserControl
{
    public OutlineToolView()
    {
        InitializeComponent();
    }

    private void OnSymbolDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is CodeSymbol symbol && DataContext is OutlineToolViewModel vm)
        {
            vm.SelectSymbol(symbol);
        }
    }
}
