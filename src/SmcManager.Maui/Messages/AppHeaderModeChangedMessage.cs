namespace SmcManager.Maui.Messages;

/// <summary>
/// Режим кнопки в шапке изменился (настройки ↔ назад).
/// </summary>
public sealed class AppHeaderModeChangedMessage(bool isSettingsVisible)
{
    public bool IsSettingsVisible { get; } = isSettingsVisible;
}
