using System;
using System.Collections.Generic;

namespace AgduBugdu.Plugin.ColorPicker;

public class ColorPickerViewModel
{
    private ColorValue _color = new();

    public string Title => "Color Palette & Picker";
    public ColorValue CurrentColor => _color;

    public byte Red
    {
        get => _color.R;
        set
        {
            if (_color.R != value)
            {
                _color.R = value;
                OnColorChanged();
            }
        }
    }

    public byte Green
    {
        get => _color.G;
        set
        {
            if (_color.G != value)
            {
                _color.G = value;
                OnColorChanged();
            }
        }
    }

    public byte Blue
    {
        get => _color.B;
        set
        {
            if (_color.B != value)
            {
                _color.B = value;
                OnColorChanged();
            }
        }
    }

    public byte Alpha
    {
        get => _color.A;
        set
        {
            if (_color.A != value)
            {
                _color.A = value;
                OnColorChanged();
            }
        }
    }

    public string Hex6 => _color.Hex6;
    public string Hex8 => _color.Hex8;
    public string HexAvalonia => _color.HexAvalonia;
    public string RgbText => _color.RgbString;
    public string RgbaText => _color.RgbaString;
    public string HslText => _color.HslString;

    public List<ColorPaletteSwatch> Swatches { get; } = new(ColorValue.GetDefaultSwatches());
    public List<ColorPaletteSwatch> RecentSwatches { get; } = new();

    public event EventHandler? ColorChanged;

    public void SelectSwatch(ColorPaletteSwatch swatch)
    {
        if (ColorValue.TryParse(swatch.HexCode, out var parsed))
        {
            _color = parsed;
            AddRecentSwatch(swatch);
            OnColorChanged();
        }
    }

    public void SetHex(string hex)
    {
        if (ColorValue.TryParse(hex, out var parsed))
        {
            _color = parsed;
            OnColorChanged();
        }
    }

    public void SetHsl(double h, double s, double l)
    {
        _color = ColorValue.FromHsl(h, s, l, _color.A);
        OnColorChanged();
    }

    public void AddCustomSwatch(string name)
    {
        var swatch = new ColorPaletteSwatch(string.IsNullOrWhiteSpace(name) ? Hex6 : name, Hex6, "Custom");
        Swatches.Insert(0, swatch);
        AddRecentSwatch(swatch);
    }

    private void AddRecentSwatch(ColorPaletteSwatch swatch)
    {
        RecentSwatches.RemoveAll(s => s.HexCode.Equals(swatch.HexCode, StringComparison.OrdinalIgnoreCase));
        RecentSwatches.Insert(0, swatch);
        if (RecentSwatches.Count > 12)
        {
            RecentSwatches.RemoveAt(RecentSwatches.Count - 1);
        }
    }

    private void OnColorChanged()
    {
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }
}
