using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Maui.Services;

/// <summary>
/// Загрузка и сортировка тегов с учётом настроек пользователя.
/// </summary>
public sealed class TagListService(IContentRepository repository, ISettingsService settings)
{
    public async Task<IReadOnlyList<ContentTag>> GetSortedTagsAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await settings.GetAppSettingsAsync();
        var tags = await repository.GetTagsAsync(cancellationToken);
        var usage = await repository.GetTagUsageCountsAsync(cancellationToken);
        return TagSorter.Sort(tags, appSettings.TagSortMode, usage);
    }

    public async Task<IReadOnlyList<ContentTag>> SortTagsAsync(
        IEnumerable<ContentTag> tags,
        CancellationToken cancellationToken = default)
    {
        var appSettings = await settings.GetAppSettingsAsync();
        var usage = await repository.GetTagUsageCountsAsync(cancellationToken);
        return TagSorter.Sort(tags, appSettings.TagSortMode, usage);
    }
}
