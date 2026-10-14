using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AgduBugdu.Plugin.ColorPicker;

public record ColorPaletteSwatch(string Name, string HexCode, string Category);

public class ColorValue
{
    public byte R { get; set; } = 59;
    public byte G { get; set; } = 130;
    public byte B { get; set; } = 246; // Default Tailwind blue-500
    public byte A { get; set; } = 255;

    public string Hex6 => $"#{R:X2}{G:X2}{B:X2}";
    public string Hex8 => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
    public string HexAvalonia => $"0x{A:X2}{R:X2}{G:X2}{B:X2}";
    public string RgbString => $"rgb({R}, {G}, {B})";
    public string RgbaString => $"rgba({R}, {G}, {B}, {A / 255.0:F2})";

    public (double H, double S, double L) ToHsl()
    {
        double r = R / 255.0;
        double g = G / 255.0;
        double b = B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double l = (max + min) / 2.0;
        double h = 0;
        double s = 0;

        if (delta != 0)
        {
            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            if (max == r)
            {
                h = ((g - b) / delta) + (g < b ? 6 : 0);
            }
            else if (max == g)
            {
                h = ((b - r) / delta) + 2;
            }
            else
            {
                h = ((r - g) / delta) + 4;
            }

            h *= 60.0;
        }

        return (h, s * 100.0, l * 100.0);
    }

    public string HslString
    {
        get
        {
            var (h, s, l) = ToHsl();
            return $"hsl({h:F0}, {s:F0}%, {l:F0}%)";
        }
    }

    public static ColorValue FromRgb(byte r, byte g, byte b, byte a = 255)
    {
        return new ColorValue { R = r, G = g, B = b, A = a };
    }

    public static ColorValue FromHsl(double h, double s, double l, byte a = 255)
    {
        s /= 100.0;
        l /= 100.0;

        double c = (1.0 - Math.Abs((2.0 * l) - 1.0)) * s;
        double x = c * (1.0 - Math.Abs(((h / 60.0) % 2) - 1.0));
        double m = l - (c / 2.0);

        double r1 = 0, g1 = 0, b1 = 0;

        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return new ColorValue
        {
            R = (byte)Math.Clamp((r1 + m) * 255.0, 0, 255),
            G = (byte)Math.Clamp((g1 + m) * 255.0, 0, 255),
            B = (byte)Math.Clamp((b1 + m) * 255.0, 0, 255),
            A = a
        };
    }

    public static bool TryParse(string input, out ColorValue color)
    {
        color = new ColorValue();
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string clean = input.Trim();

        // 1. Hex parsing e.g. #RGB, #RGBA, #RRGGBB, #AARRGGBB, 0xFFRRGGBB
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[2..];
        }
        else if (clean.StartsWith("#"))
        {
            clean = clean[1..];
        }

        if (clean.Length == 3 && uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            byte r = Convert.ToByte(new string(clean[0], 2), 16);
            byte g = Convert.ToByte(new string(clean[1], 2), 16);
            byte b = Convert.ToByte(new string(clean[2], 2), 16);
            color = FromRgb(r, g, b);
            return true;
        }

        if (clean.Length == 6 && uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            byte r = byte.Parse(clean[0..2], NumberStyles.HexNumber);
            byte g = byte.Parse(clean[2..4], NumberStyles.HexNumber);
            byte b = byte.Parse(clean[4..6], NumberStyles.HexNumber);
            color = FromRgb(r, g, b);
            return true;
        }

        if (clean.Length == 8 && uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            byte a = byte.Parse(clean[0..2], NumberStyles.HexNumber);
            byte r = byte.Parse(clean[2..4], NumberStyles.HexNumber);
            byte g = byte.Parse(clean[4..6], NumberStyles.HexNumber);
            byte b = byte.Parse(clean[6..8], NumberStyles.HexNumber);
            color = FromRgb(r, g, b, a);
            return true;
        }

        // 2. rgb(r, g, b)
        var rgbMatch = Regex.Match(input, @"rgb\s*\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)", RegexOptions.IgnoreCase);
        if (rgbMatch.Success)
        {
            byte r = byte.Parse(rgbMatch.Groups[1].Value);
            byte g = byte.Parse(rgbMatch.Groups[2].Value);
            byte b = byte.Parse(rgbMatch.Groups[3].Value);
            color = FromRgb(r, g, b);
            return true;
        }

        return false;
    }

    public static List<ColorPaletteSwatch> GetDefaultSwatches()
    {
        return new List<ColorPaletteSwatch>
        {
            new("Slate 500", "#64748B", "Tailwind"),
            new("Red 500", "#EF4444", "Tailwind"),
            new("Orange 500", "#F97316", "Tailwind"),
            new("Amber 500", "#F59E0B", "Tailwind"),
            new("Emerald 500", "#10B981", "Tailwind"),
            new("Cyan 500", "#06B6D4", "Tailwind"),
            new("Blue 500", "#3B82F6", "Tailwind"),
            new("Indigo 500", "#6366F1", "Tailwind"),
            new("Purple 500", "#A855F7", "Tailwind"),
            new("Pink 500", "#EC4899", "Tailwind"),
            new("Nord Polar", "#2E3440", "Nord"),
            new("Nord Frost", "#88C0D0", "Nord"),
            new("Nord Aurora", "#A3BE8C", "Nord"),
            new("Monokai Pink", "#F92672", "Monokai"),
            new("Monokai Green", "#A6E22E", "Monokai"),
            new("Monokai Orange", "#FD971F", "Monokai"),
            new("Monokai Purple", "#AE81FF", "Monokai")
        };
    }
}
