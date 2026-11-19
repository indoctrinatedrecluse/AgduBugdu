using System;
using System.IO;
using AgduBugdu.Plugin.HexViewer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class HexViewerToolViewModel : Tool
{
    private HexViewerViewModel? _model;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private string _fileName = "No File Loaded";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _formattedDumpText = "Select a file to inspect its binary hex dump.";

    [ObservableProperty]
    private string _pageSummary = "Page 1 / 1 (Offset: 0x00000000)";

    [ObservableProperty]
    private string _statsSummary = "No file inspected";

    [ObservableProperty]
    private string _targetOffsetHex = "0";

    public HexViewerToolViewModel()
    {
        Id = "HexViewer";
        Title = "Hex Inspector";
    }

    public void AttachModel(HexViewerViewModel model)
    {
        _model = model;
        _model.FileLoaded += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromModel);
        _model.PageChanged += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromModel);
        SyncFromModel();
    }

    public void LoadFile(string path)
    {
        if (_model == null) _model = new HexViewerViewModel();
        _model.LoadFile(path);
        SyncFromModel();
    }

    [RelayCommand]
    public void NextPage()
    {
        _model?.NextPage();
        SyncFromModel();
    }

    [RelayCommand]
    public void PreviousPage()
    {
        _model?.PreviousPage();
        SyncFromModel();
    }

    [RelayCommand]
    public void GoToOffset()
    {
        if (_model == null) return;
        string clean = TargetOffsetHex.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        if (long.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out long offset) ||
            long.TryParse(clean, out offset))
        {
            _model.GoToOffset(offset);
            SyncFromModel();
        }
    }

    private void SyncFromModel()
    {
        if (_model == null) return;

        FileName = _model.FileName;
        FilePath = _model.LoadedFilePath ?? string.Empty;
        FormattedDumpText = _model.FormattedDumpText;
        PageSummary = _model.PageSummary;
        StatsSummary = _model.Stats.SummaryText;
    }

    [RelayCommand]
    public void ResetSize() => ResetSizeRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void Minimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);
}
