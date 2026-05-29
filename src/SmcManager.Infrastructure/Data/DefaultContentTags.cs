using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Data;

/// <summary>
/// Стартовые теги для сортировки скачанного контента.
/// </summary>
public static class DefaultContentTags
{
    public static IReadOnlyList<DefaultTagDefinition> All { get; } =
    [
        new("🎌 Anime", "#E91E63", 0),
        new("💻 IT", "#2196F3", 1),
        new("🎬 Films", "#673AB7", 2),
        new("😂 Мемы", "#FF9800", 3),
        new("💪 Fitness", "#43A047", 4),
        new("💚 Здоровье", "#66BB6A", 5),
        new("🎨 Art", "#AB47BC", 6),
        new("👗 Одежда", "#EC407A", 7),
        new("✨ Стиль", "#9C27B0", 8),
        new("📚 Обучение", "#3F51B5", 9),
        new("🍳 Кулинария", "#FF7043", 10),
        new("🖌️ Дизайн", "#26A69A", 11),
        new("🍕 Еда", "#FB8C00", 12),
        new("⭐ Избранное", "#FFD700", 13),
        new("📝 Рецепты", "#F4511E", 14),
        new("📸 Творчество", "#8E24AA", 15),
        new("✈️ Путешествие", "#29B6F6", 16),
        new("💡 Лайфхаки", "#00BCD4", 17),
        new("💼 Работа", "#546E7A", 18),
        new("🤣 Юмор", "#42A5F5", 19),
        new("🚗 Авто", "#78909C", 20)
    ];

    public sealed record DefaultTagDefinition(string Name, string ColorHex, int SortOrder)
    {
        public ContentTag ToEntity(DateTime createdAtUtc) => new()
        {
            Name = Name,
            ColorHex = ColorHex,
            SortOrder = SortOrder,
            CreatedAt = createdAtUtc
        };
    }
}
