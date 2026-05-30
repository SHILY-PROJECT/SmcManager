namespace SmcManager.Maui.Services;

/// <summary>
/// Контекст модального выбора цвета тега.
/// </summary>
public sealed class TagColorPickerNavigationContext
{
    public static TagColorPickerNavigationContext? Current { get; set; }

    public required string InitialColor { get; init; }

    public required TaskCompletionSource<string?> Completion { get; init; }

    /// <summary>Вызывается при «Готово» до закрытия модального окна (чтобы сразу обновить swatch).</summary>
    public Action<string>? OnColorSelected { get; init; }

    public bool IsFinished { get; set; }
}
