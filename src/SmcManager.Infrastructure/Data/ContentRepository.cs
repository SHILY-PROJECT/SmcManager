using Microsoft.EntityFrameworkCore;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

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
        await MigrateContentItemTagsAsync(cancellationToken);
        await MigrateContentTagsMetadataAsync(cancellationToken);
    }

    private async Task MigrateContentTagsMetadataAsync(CancellationToken cancellationToken)
    {
        var columnNames = await GetTableColumnNamesAsync("Tags", cancellationToken);
        if (columnNames.Count == 0)
            return;

        if (!columnNames.Contains("SortOrder", StringComparer.OrdinalIgnoreCase))
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Tags ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 1000;",
                cancellationToken);
        }

        if (!columnNames.Contains("CreatedAt", StringComparer.OrdinalIgnoreCase))
        {
            await _db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Tags ADD COLUMN CreatedAt TEXT NULL;",
                cancellationToken);
        }

        var utcNow = DateTime.UtcNow.ToString("O");
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE Tags
            SET CreatedAt = {0}
            WHERE CreatedAt IS NULL OR CreatedAt = '';
            """,
            [utcNow],
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetTableColumnNamesAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var columnNames = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columnNames.Add(reader.GetString(1));

        return columnNames;
    }

    private async Task MigrateContentItemTagsAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var columnNames = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('ContentItemTags');";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                columnNames.Add(reader.GetString(1));
        }

        if (columnNames.Contains("ContentItemsId", StringComparer.OrdinalIgnoreCase))
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE ContentItemTags_fixed (
                    ContentItemId INTEGER NOT NULL,
                    TagsId INTEGER NOT NULL,
                    PRIMARY KEY (ContentItemId, TagsId),
                    FOREIGN KEY (ContentItemId) REFERENCES ContentItems(Id) ON DELETE CASCADE,
                    FOREIGN KEY (TagsId) REFERENCES Tags(Id) ON DELETE CASCADE
                );

                INSERT OR IGNORE INTO ContentItemTags_fixed (ContentItemId, TagsId)
                SELECT ContentItemsId, TagsId FROM ContentItemTags;

                DROP TABLE ContentItemTags;
                ALTER TABLE ContentItemTags_fixed RENAME TO ContentItemTags;
                """,
                cancellationToken);
        }
        else if (columnNames.Count == 0)
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE ContentItemTags (
                    ContentItemId INTEGER NOT NULL,
                    TagsId INTEGER NOT NULL,
                    PRIMARY KEY (ContentItemId, TagsId),
                    FOREIGN KEY (ContentItemId) REFERENCES ContentItems(Id) ON DELETE CASCADE,
                    FOREIGN KEY (TagsId) REFERENCES Tags(Id) ON DELETE CASCADE
                );
                """,
                cancellationToken);
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                INSERT OR IGNORE INTO ContentItemTags (ContentItemId, TagsId)
                SELECT Id, TagId
                FROM ContentItems
                WHERE TagId IS NOT NULL;
                """,
                cancellationToken);
        }
        catch
        {
            // legacy TagId column missing or already migrated
        }
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
        await RemoveLegacyDefaultTagsAsync(cancellationToken);

        var existingTags = await _db.Tags.ToListAsync(cancellationToken);
        var existingByName = existingTags.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var catalogNames = new HashSet<string>(
            DefaultContentTags.All.Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);
        var utcNow = DateTime.UtcNow;

        var toAdd = new List<ContentTag>();
        foreach (var definition in DefaultContentTags.All)
        {
            if (existingByName.TryGetValue(definition.Name, out var existing))
            {
                if (existing.SortOrder != definition.SortOrder)
                    existing.SortOrder = definition.SortOrder;

                continue;
            }

            toAdd.Add(definition.ToEntity(utcNow));
        }

        foreach (var tag in existingTags)
        {
            if (tag.SortOrder < 1000 && !catalogNames.Contains(tag.Name))
                tag.SortOrder = 1000 + tag.Id;
        }

        if (toAdd.Count > 0)
            _db.Tags.AddRange(toAdd);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveLegacyDefaultTagsAsync(CancellationToken cancellationToken)
    {
        var newNames = new HashSet<string>(
            DefaultContentTags.All.Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);

        var legacyNames = LegacyDefaultContentTags.Names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legacyTags = await _db.Tags.ToListAsync(cancellationToken);
        var toRemove = legacyTags
            .Where(t => legacyNames.Contains(t.Name) && !newNames.Contains(t.Name))
            .ToList();

        if (toRemove.Count == 0)
            return;

        _db.Tags.RemoveRange(toRemove);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<ContentItem?> GetContentByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> GetAllContentAsync(CancellationToken cancellationToken = default) =>
        await _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tags)
            .OrderByDescending(c => c.DownloadedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentItem>> GetContentByTagAsync(int tagId, CancellationToken cancellationToken = default) =>
        await _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tags)
            .Where(c => c.Tags.Any(t => t.Id == tagId))
            .OrderByDescending(c => c.DownloadedAt)
            .ToListAsync(cancellationToken);

    public Task<ContentItem?> GetLatestContentAsync(CancellationToken cancellationToken = default) =>
        _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tags)
            .OrderByDescending(c => c.DownloadedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ContentItem?> GetContentBySourceUrlAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return null;

        var normalized = ContentUrlNormalizer.Normalize(sourceUrl.Trim());
        var items = await _db.ContentItems
            .AsNoTracking()
            .Where(c => c.SourceUrl != null && c.SourceUrl != string.Empty)
            .ToListAsync(cancellationToken);

        return items.FirstOrDefault(c =>
            ContentUrlNormalizer.Normalize(c.SourceUrl) == normalized);
    }

    public async Task<IReadOnlyList<ContentItem>> GetRecentContentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        return await _db.ContentItems
            .Include(c => c.MediaFiles)
            .Include(c => c.Tags)
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

    public async Task AssignTagsAsync(
        int contentId,
        IReadOnlyList<int> tagIds,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.ContentItems
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == contentId, cancellationToken);

        if (item is null)
            return;

        item.Tags.Clear();

        if (tagIds.Count > 0)
        {
            var tags = await _db.Tags
                .Where(t => tagIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

            foreach (var tag in tags)
                item.Tags.Add(tag);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentTag>> GetTagsAsync(CancellationToken cancellationToken = default) =>
        await _db.Tags.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetTagUsageCountsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.ContentItems
            .AsNoTracking()
            .SelectMany(c => c.Tags.Select(t => t.Id))
            .GroupBy(id => id)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TagId, x => x.Count, cancellationToken);
    }

    public async Task<ContentTag> SaveTagAsync(ContentTag tag, CancellationToken cancellationToken = default)
    {
        if (tag.Id == 0)
        {
            if (tag.CreatedAt == default)
                tag.CreatedAt = DateTime.UtcNow;

            if (tag.SortOrder <= 0)
            {
                var maxSort = await _db.Tags.MaxAsync(t => (int?)t.SortOrder, cancellationToken) ?? 999;
                tag.SortOrder = Math.Max(maxSort + 1, 1000);
            }

            _db.Tags.Add(tag);
        }
        else
        {
            var existing = await _db.Tags.FindAsync([tag.Id], cancellationToken);
            if (existing is null)
                return tag;

            existing.Name = tag.Name;
            existing.ColorHex = tag.ColorHex;
            existing.SortOrder = tag.SortOrder;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

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
        _db.ContentItems.CountAsync(c => c.Tags.Any(t => t.Id == tagId), cancellationToken);

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
