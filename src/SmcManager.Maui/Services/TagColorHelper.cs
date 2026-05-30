namespace SmcManager.Maui.Services;

/// <summary>
/// HSL-палитра и преобразование цветов тегов.
/// </summary>
public static class TagColorHelper
{
    public static IReadOnlyList<string> Palette { get; } = BuildPalette();

    public static string DefaultHex => TagColorPresets.Default;

    public static string NormalizeHex(string? hex)
    {
        if (TryParseHex(hex, out var color))
            return ToHex(color);

        return DefaultHex;
    }

    public static string ToHex(Color color)
    {
        var r = (int)Math.Round(color.Red * 255);
        var g = (int)Math.Round(color.Green * 255);
        var b = (int)Math.Round(color.Blue * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static bool TryParseHex(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        try
        {
            color = Color.FromArgb(hex);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Color FromHsl(double hue, double saturation, double lightness) =>
        Color.FromRgb(
            ChannelFromHue(hue, saturation, lightness, 0),
            ChannelFromHue(hue, saturation, lightness, 8),
            ChannelFromHue(hue, saturation, lightness, 4));

    public static (double Hue, double Saturation, double Lightness) ToHsl(Color color)
    {
        var r = color.Red;
        var g = color.Green;
        var b = color.Blue;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.0001)
            return (0, 0, lightness * 100);

        var delta = max - min;
        var saturation = lightness > 0.5
            ? delta / (2.0 - max - min)
            : delta / (max + min);

        double hue;
        if (Math.Abs(max - r) < 0.0001)
            hue = ((g - b) / delta + (g < b ? 6 : 0)) * 60;
        else if (Math.Abs(max - g) < 0.0001)
            hue = ((b - r) / delta + 2) * 60;
        else
            hue = ((r - g) / delta + 4) * 60;

        return (hue, saturation * 100, lightness * 100);
    }

    private static IReadOnlyList<string> BuildPalette()
    {
        var colors = new List<string>();
        for (var hue = 0; hue < 360; hue += 15)
        {
            for (var lightness = 88; lightness >= 28; lightness -= 12)
            {
                var saturation = lightness > 70 ? 0.55 : 0.82;
                colors.Add(ToHex(FromHsl(hue, saturation, lightness / 100.0)));
            }
        }

        return colors;
    }

    private static float ChannelFromHue(double hue, double saturation, double lightness, int shift)
    {
        var h = (hue % 360 + 360) % 360 / 60.0;
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var m = lightness - c / 2;

        double r1, g1, b1;
        if (h < 1)
            (r1, g1, b1) = (c, x, 0);
        else if (h < 2)
            (r1, g1, b1) = (x, c, 0);
        else if (h < 3)
            (r1, g1, b1) = (0, c, x);
        else if (h < 4)
            (r1, g1, b1) = (0, x, c);
        else if (h < 5)
            (r1, g1, b1) = (x, 0, c);
        else
            (r1, g1, b1) = (c, 0, x);

        return shift switch
        {
            0 => (float)(r1 + m),
            8 => (float)(g1 + m),
            _ => (float)(b1 + m)
        };
    }
}
