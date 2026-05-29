namespace SmcManager.Core.Models;

/// <summary>
/// Тег для группировки контента (здоровье, еда, спорт и т.д.).
/// </summary>
public class ContentTag
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#7C4DFF";

    /// <summary>Порядок в каталоге (меньше — выше при сортировке «по умолчанию»).</summary>
    public int SortOrder { get; set; } = 1000;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
