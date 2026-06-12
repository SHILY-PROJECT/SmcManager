using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Services;

/// <summary>
/// Единая сортировка тегов для всех экранов.
/// </summary>
public static class TagSorter
{
    public static IReadOnlyList<ContentTag> Sort(
        IEnumerable<ContentTag> tags,
        TagSortMode mode,
        IReadOnlyDictionary<int, int>? usageCounts = null)
    {
        var list = tags.ToList();
        usageCounts ??= new Dictionary<int, int>();

        return mode switch
        {
            TagSortMode.UsageCount => list
                .OrderByDescending(t => usageCounts.GetValueOrDefault(t.Id))
                .ThenBy(t => t.SortOrder)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TagSortMode.DateAdded => list
                .OrderByDescending(t => t.CreatedAt)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TagSortMode.Name => list
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => list
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
