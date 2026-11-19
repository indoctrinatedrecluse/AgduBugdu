using System;
using System.Collections.ObjectModel;
using AgduBugdu.Plugin.ColorPicker;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Tools;

public partial class ColorPickerToolViewModel : Tool
{
    private ColorPickerViewModel? _model;

    public event EventHandler? ResetSizeRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler<string>? InsertColorRequested;

    [ObservableProperty]
    private byte _red = 56;

    [ObservableProperty]
    private byte _green = 189;

    [ObservableProperty]
    private byte _blue = 248;

    [ObservableProperty]
    private byte _alpha = 255;

    [ObservableProperty]
    private string _hex6 = "#38BDF8";

    [ObservableProperty]
    private string _hex8 = "#FF38BDF8";

    [ObservableProperty]
    private string _rgbText = "rgb(56, 189, 248)";

    [ObservableProperty]
    private string _hslText = "hsl(198, 93%, 60%)";

    [ObservableProperty]
    private string _colorPreviewHex = "#FF38BDF8";

    public ObservableCollection<ColorPaletteSwatch> Swatches { get; } = new();

    public ColorPickerToolViewModel()
    {
        Id = "ColorPicker";
        Title = "Color Picker";
    }

    public void AttachModel(ColorPickerViewModel model)
    {
        _model = model;
        _model.ColorChanged += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromModel);
        SyncFromModel();
    }

    partial void OnRedChanged(byte value)
    {
        if (_model != null && _model.Red != value)
        {
            _model.Red = value;
            SyncFromModel();
        }
    }

    partial void OnGreenChanged(byte value)
    {
        if (_model != null && _model.Green != value)
        {
            _model.Green = value;
            SyncFromModel();
        }
    }

    partial void OnBlueChanged(byte value)
    {
        if (_model != null && _model.Blue != value)
        {
            _model.Blue = value;
            SyncFromModel();
        }
    }

    partial void OnAlphaChanged(byte value)
    {
        if (_model != null && _model.Alpha != value)
        {
            _model.Alpha = value;
            SyncFromModel();
        }
    }

    [RelayCommand]
    public void SelectSwatch(ColorPaletteSwatch? swatch)
    {
        if (swatch == null) return;
        _model?.SelectSwatch(swatch);
        SyncFromModel();
    }

    [RelayCommand]
    public void InsertColor(string? colorFormat)
    {
        string color = colorFormat switch
        {
            "rgb" => RgbText,
            "hsl" => HslText,
            "hex8" => Hex8,
            _ => Hex6
        };
        InsertColorRequested?.Invoke(this, color);
    }

    private void SyncFromModel()
    {
        if (_model == null) return;

        if (Red != _model.Red) Red = _model.Red;
        if (Green != _model.Green) Green = _model.Green;
        if (Blue != _model.Blue) Blue = _model.Blue;
        if (Alpha != _model.Alpha) Alpha = _model.Alpha;

        Hex6 = _model.Hex6;
        Hex8 = _model.Hex8;
        RgbText = _model.RgbText;
        HslText = _model.HslText;
        ColorPreviewHex = $"#{Alpha:X2}{Red:X2}{Green:X2}{Blue:X2}";

        Swatches.Clear();
        foreach (var s in _model.Swatches)
        {
            Swatches.Add(s);
        }
    }

    [RelayCommand]
    public void ResetSize() => ResetSizeRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void Minimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);
}
