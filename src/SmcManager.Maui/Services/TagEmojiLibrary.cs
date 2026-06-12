namespace SmcManager.Maui.Services;

/// <summary>
/// Подборка эмодзи для категорий и быстрого создания тегов.
/// </summary>
public static class TagEmojiLibrary
{
    public static IReadOnlyList<string> Suggested { get; } =
    [
        "🎌", "💻", "🎬", "😂", "🤣", "💪", "💚", "🎨", "👗", "✨",
        "📚", "🍳", "🖌️", "🍕", "🍔", "🍣", "⭐", "📝", "📸", "🎵",
        "✈️", "🏖️", "💡", "💼", "🚗", "🏋️", "🧘", "🎮", "🐾", "🏠",
        "💰", "👨‍👩‍👧", "📦", "🔥", "❤️", "🌿", "☕", "🎁", "🛠️", "📱"
    ];
}
