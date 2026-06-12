using SmcManager.Core.Enums;

namespace SmcManager.Maui.Models;

/// <summary>
/// Пункт выбора сортировки тегов в настройках.
/// </summary>
public sealed class TagSortOption
{
    public required TagSortMode Mode { get; init; }

    public required string Title { get; init; }
}
