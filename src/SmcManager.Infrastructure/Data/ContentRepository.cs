using Microsoft.EntityFrameworkCore;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Data;

/// <summary>
/// EF Core реализация репозитория контента.
/// </summary>
public class ContentRepository : IContentRepository
{
    private readonly AppDbContext _db;
    private readonly string _mediaRootPath;

    public ContentRepository(AppDbContext db, string mediaRootPath)
    {
        _db = db;
        _mediaRootPath = mediaRootPath;
    }

    private async Task EnsureSchemaUpToDateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ContentItems ADD COLUMN PostedAt TEXT NULL;", cancellationToken);
        }
        catch
        {
            // column exists
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ContentItems ADD COLUMN StorageRelativePath TEXT NULL;", cancellationToken);
        }
        catch
        {
            // column exists
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ContentItems ADD COLUMN UserComment TEXT NULL;", cancellationToken);
        }
        catch
        {
            // column exists
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SocialAccounts ADD COLUMN DisplayName TEXT NULL;", cancellationToken);
        }
        catch { }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SocialAccounts ADD COLUMN Cookies TEXT NULL;", cancellationToken);
        }
        catch { }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SocialAccounts ADD COLUMN IsDefault INTEGER NOT NULL DEFAULT 0;", cancellationToken);
        }
        catch { }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SocialAccounts ADD COLUMN AuthMethod INTEGER NOT NULL DEFAULT 0;", cancellationToken);
        }
        catch { }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SocialAccounts ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;", cancellationToken);
        }
        catch { }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                UPDATE SocialAccounts
                SET IsActive = 1
                WHERE IsActive = 0
                  AND (Cookies IS NOT NULL OR SessionToken IS NOT NULL);
                """,
                cancellationToken);
        }
        catch { }

        await MigrateLegacySessionTokensAsync(cancellationToken);
    }

    private async Task MigrateLegacySessionTokensAsync(CancellationToken cancellationToken)
    {
        var accounts = await _db.SocialAccounts
            .Where(a => a.SessionToken != null && a.Cookies == null)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0) return;

        foreach (var account in accounts)
        {
            account.Cookies = account.SessionToken;
            account.SessionToken = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSchemaUpToDateAsync(cancellationToken);

        await EnsureDefaultTagsAsync(cancellationToken);
    }

    private async Task EnsureDefaultTagsAsync(CancellationToken cancellationToken)
    {
        var existingNames = await _db.Tags
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var toAdd = DefaultContentTags.All
            .Where(t => !existingSet.Contains(t.Name))
            .Select(t => new ContentTag { Name = t.Name, ColorHex = t.ColorHex })
            .ToList();

        if (toAdd.Count == 0) return;

        _db.Tags.AddRange(toAdd);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<ContentItem?> GetContentByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tag)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> GetAllContentAsync(CancellationToken cancellationToken = default) =>
        await _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tag)
            .OrderByDescending(c => c.DownloadedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> GetContentByTagAsync(int tagId, CancellationToken cancellationToken = default) =>
        await _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tag)
            .Where(c => c.TagId == tagId)
            .OrderByDescending(c => c.DownloadedAt)
            .ToListAsync(cancellationToken);

    public Task<ContentItem?> GetLatestContentAsync(CancellationToken cancellationToken = default) =>
        _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tag)
            .OrderByDescending(c => c.DownloadedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> GetRecentContentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        return await _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tag)
            .OrderByDescending(c => c.DownloadedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentItem> SaveContentAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        if (item.Id == 0)
            _db.ContentItems.Add(item);
        else
            _db.ContentItems.Update(item);

        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task DeleteContentAsync(int contentId, CancellationToken cancellationToken = default)
    {
        var item = await _db.ContentItems
            .Include(c => c.MediaFiles)
            .FirstOrDefaultAsync(c => c.Id == contentId, cancellationToken);

        if (item is null) return;

        var localPaths = item.MediaFiles
            .Select(m => m.LocalPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        _db.ContentItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var path in localPaths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore IO errors
            }
        }

        if (!string.IsNullOrWhiteSpace(item.StorageRelativePath))
        {
            var folder = Path.Combine(_mediaRootPath, item.StorageRelativePath);
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    public async Task AssignTagAsync(int contentId, int? tagId, CancellationToken cancellationToken = default)
    {
        var item = await _db.ContentItems.FindAsync([contentId], cancellationToken);
        if (item is null) return;
        item.TagId = tagId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentTag>> GetTagsAsync(CancellationToken cancellationToken = default) =>
        await _db.Tags.OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public async Task<ContentTag> SaveTagAsync(ContentTag tag, CancellationToken cancellationToken = default)
    {
        if (tag.Id == 0)
            _db.Tags.Add(tag);
        else
            _db.Tags.Update(tag);
        await _db.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task DeleteTagAsync(int tagId, CancellationToken cancellationToken = default)
    {
        var tag = await _db.Tags.FindAsync([tagId], cancellationToken);
        if (tag is null) return;
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountContentByTagAsync(int tagId, CancellationToken cancellationToken = default) =>
        _db.ContentItems.CountAsync(c => c.TagId == tagId, cancellationToken);

    public async Task<IReadOnlyList<SocialAccount>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        await _db.SocialAccounts.OrderBy(a => a.Platform).ThenBy(a => a.Username)
            .ToListAsync(cancellationToken);

    public async Task<SocialAccount> SaveAccountAsync(SocialAccount account, CancellationToken cancellationToken = default)
    {
        if (account.Id == 0)
            _db.SocialAccounts.Add(account);
        else
            _db.SocialAccounts.Update(account);
        await _db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task DeleteAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await _db.SocialAccounts.FindAsync([accountId], cancellationToken);
        if (account is null) return;
        _db.SocialAccounts.Remove(account);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
