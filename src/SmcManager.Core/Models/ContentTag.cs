namespace SmcManager.Core.Models;

/// <summary>
/// Тег для группировки контента (здоровье, еда, спорт и т.д.).
/// </summary>
public class ContentTag
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#7C4DFF";
}
