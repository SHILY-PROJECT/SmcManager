using SmcManager.Maui.Services;

namespace SmcManager.Maui.Messages;

/// <summary>
/// Тема приложения изменена — обновить иконки в UI.
/// </summary>
public sealed class ThemeChangedMessage(ThemePalette palette)
{
    public ThemePalette Palette { get; } = palette;
}
