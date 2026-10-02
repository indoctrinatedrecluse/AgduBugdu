using Avalonia.Controls;

namespace AgduBugdu.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (s, e) => { if (DataContext is System.IDisposable d) d.Dispose(); };
    }
}
