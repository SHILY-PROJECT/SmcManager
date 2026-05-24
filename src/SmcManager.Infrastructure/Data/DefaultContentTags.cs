using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Data;

/// <summary>
/// Стартовые теги для сортировки скачанного контента.
/// </summary>
public static class DefaultContentTags
{
    public static IReadOnlyList<ContentTag> All { get; } =
    [
        new() { Name = "⭐ Избранное", ColorHex = "#FFD700" },
        new() { Name = "🍕 Еда", ColorHex = "#FF9800" },
        new() { Name = "💪 Спорт", ColorHex = "#2196F3" },
        new() { Name = "✈️ Путешествия", ColorHex = "#9C27B0" },
        new() { Name = "💚 Здоровье", ColorHex = "#4CAF50" },
        new() { Name = "🎬 Кино и сериалы", ColorHex = "#4A8FE7" },
        new() { Name = "🎵 Музыка", ColorHex = "#3A7BD5" },
        new() { Name = "📚 Обучение", ColorHex = "#3F51B5" },
        new() { Name = "💼 Работа", ColorHex = "#455A64" },
        new() { Name = "😂 Юмор", ColorHex = "#6BA3F0" },
        new() { Name = "👗 Мода", ColorHex = "#4A8FE7" },
        new() { Name = "🏠 Дом и интерьер", ColorHex = "#795548" },
        new() { Name = "🐾 Животные", ColorHex = "#8BC34A" },
        new() { Name = "💡 Лайфхаки", ColorHex = "#00BCD4" },
        new() { Name = "🎮 Игры", ColorHex = "#673AB7" },
        new() { Name = "👨‍👩‍👧 Семья", ColorHex = "#FF5722" },
        new() { Name = "💰 Финансы", ColorHex = "#009688" },
        new() { Name = "📸 Творчество", ColorHex = "#AB47BC" },
        new() { Name = "🚗 Авто", ColorHex = "#546E7A" },
        new() { Name = "🧘 Wellness", ColorHex = "#66BB6A" },
        new() { Name = "📦 Другое", ColorHex = "#607D8B" }
    ];
}
