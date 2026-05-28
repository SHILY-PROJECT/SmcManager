using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Репозиторий для CRUD скачанного контента, тегов и аккаунтов.
/// </summary>
public interface IContentRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<ContentItem?> GetContentByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItem>> GetAllContentAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItem>> GetContentByTagAsync(int tagId, CancellationToken cancellationToken = default);

    Task<ContentItem?> GetLatestContentAsync(CancellationToken cancellationToken = default);

    Task<ContentItem?> GetContentBySourceUrlAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItem>> GetRecentContentAsync(int count, CancellationToken cancellationToken = default);

    Task<ContentItem> SaveContentAsync(ContentItem item, CancellationToken cancellationToken = default);

    Task DeleteContentAsync(int contentId, CancellationToken cancellationToken = default);

    Task AssignTagAsync(int contentId, int? tagId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentTag>> GetTagsAsync(CancellationToken cancellationToken = default);

    Task<ContentTag> SaveTagAsync(ContentTag tag, CancellationToken cancellationToken = default);

    Task DeleteTagAsync(int tagId, CancellationToken cancellationToken = default);

    Task<int> CountContentByTagAsync(int tagId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<SocialAccount> SaveAccountAsync(SocialAccount account, CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(int accountId, CancellationToken cancellationToken = default);
}
