using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public class ExplorerToolViewModel : Tool
{
    public ExplorerToolViewModel()
    {
        Id = "Explorer";
        Title = "Explorer";
    }
}

public class OutputToolViewModel : Tool
{
    public OutputToolViewModel()
    {
        Id = "Output";
        Title = "Output";
    }
}
