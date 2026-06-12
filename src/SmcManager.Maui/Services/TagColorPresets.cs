namespace SmcManager.Maui.Services;

/// <summary>
/// Палитра цветов для пользовательских тегов.
/// </summary>
public static class TagColorPresets
{
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#FFFFFF",
        "#4A8FE7",
        "#6BA3F0",
        "#3A7BD5",
        "#4CAF50",
        "#2196F3",
        "#9C27B0",
        "#FF9800",
        "#00BCD4",
        "#E91E63",
        "#607D8B"
    ];

    public static string Default => "#FFFFFF";
}
