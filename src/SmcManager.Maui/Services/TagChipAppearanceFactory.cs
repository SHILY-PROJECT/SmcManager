namespace SmcManager.Maui.Services;

/// <summary>
/// Цвета чипа тега: фон, обводка и текст.
/// </summary>
public readonly record struct TagChipAppearance(Color Fill, Color Stroke, Color Text);

/// <summary>
/// Строит цвета чипа из hex тега или из темы (если цвет не задан).
/// </summary>
public static class TagChipAppearanceFactory
{
    public static TagChipAppearance For(string? colorHex, bool isSelected)
    {
        if (!TryParseColor(colorHex, out var accent))
            return FromTheme(isSelected);

        if (isSelected)
            return new TagChipAppearance(accent, accent, ContrastTextFor(accent));

        var (_, _, lightness) = TagColorHelper.ToHsl(accent);
        if (lightness >= 82)
        {
            var stroke = Application.Current?.Resources.TryGetValue("Divider", out var divider) == true
                && divider is Color dividerColor
                ? dividerColor
                : Colors.Gray;

            return new TagChipAppearance(accent.WithAlpha(0.2f), stroke, Colors.Black);
        }

        return new TagChipAppearance(
            accent.WithAlpha(0.18f),
            accent.WithAlpha(0.45f),
            accent);
    }

    private static Color ContrastTextFor(Color fill)
    {
        var (_, _, lightness) = TagColorHelper.ToHsl(fill);
        return lightness >= 70 ? Colors.Black : Colors.White;
    }

    private static TagChipAppearance FromTheme(bool isSelected)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
            return new TagChipAppearance(Colors.LightGray, Colors.Gray, Colors.Black);

        if (isSelected)
        {
            return new TagChipAppearance(
                (Color)resources["TagChipSelectedFill"],
                (Color)resources["TagChipSelectedFill"],
                (Color)resources["TagChipSelectedText"]);
        }

        return new TagChipAppearance(
            (Color)resources["TagChipFill"],
            (Color)resources["TagChipStroke"],
            (Color)resources["TagChipText"]);
    }

    private static bool TryParseColor(string? colorHex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(colorHex))
            return false;

        try
        {
            color = Color.FromArgb(colorHex);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
