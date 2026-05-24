using Microsoft.EntityFrameworkCore;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Data;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// CRUD аккаунтов и назначение сессии по умолчанию.
/// </summary>
public class SocialAccountService : ISocialAccountService
{
    private readonly AppDbContext _db;
    private readonly IContentRepository _repository;

    public SocialAccountService(AppDbContext db, IContentRepository repository)
    {
        _db = db;
        _repository = repository;
    }

    public async Task<IReadOnlyList<SocialAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        return await _repository.GetAccountsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SocialAccount>> GetAccountsForPlatformAsync(
        SocialPlatform platform,
        CancellationToken cancellationToken = default)
    {
        var all = await GetAccountsAsync(cancellationToken);
        return all.Where(a => a.Platform == platform && a.IsActive).ToList();
    }

    public async Task<SocialAccount?> GetAccountByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        return await _db.SocialAccounts.FindAsync([id], cancellationToken);
    }

    public async Task<SocialAccount?> GetDefaultAccountAsync(
        SocialPlatform platform,
        CancellationToken cancellationToken = default)
    {
        var accounts = await GetAccountsForPlatformAsync(platform, cancellationToken);
        return accounts.FirstOrDefault(a => a.IsDefault)
               ?? accounts.FirstOrDefault(a => SocialAccountAuth.HasAuth(a));
    }

    public async Task<SocialAccount> SaveAccountAsync(SocialAccount account, CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(account.Cookies))
        {
            account.Cookies = SocialAccountAuth.NormalizeAuthInput(account.Platform, account.Cookies);
            account.SessionToken = null;
        }
        else if (!string.IsNullOrWhiteSpace(account.SessionToken))
        {
            account.Cookies = SocialAccountAuth.NormalizeAuthInput(account.Platform, account.SessionToken);
            account.SessionToken = null;
        }

        var saved = await _repository.SaveAccountAsync(account, cancellationToken);

        if (saved.IsDefault)
            await SetDefaultAccountAsync(saved.Platform, saved.Id, cancellationToken);
        else
        {
            var hasDefault = (await GetAccountsForPlatformAsync(saved.Platform, cancellationToken))
                .Any(a => a.IsDefault);
            if (!hasDefault)
                await SetDefaultAccountAsync(saved.Platform, saved.Id, cancellationToken);
        }

        return saved;
    }

    public async Task DeleteAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        var account = await _db.SocialAccounts.FindAsync([accountId], cancellationToken);
        if (account is null) return;

        var wasDefault = account.IsDefault;
        var platform = account.Platform;

        await _repository.DeleteAccountAsync(accountId, cancellationToken);

        if (wasDefault)
        {
            var next = (await GetAccountsForPlatformAsync(platform, cancellationToken)).FirstOrDefault();
            if (next is not null)
                await SetDefaultAccountAsync(platform, next.Id, cancellationToken);
        }
    }

    public async Task SetDefaultAccountAsync(
        SocialPlatform platform,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);

        var accounts = await _db.SocialAccounts
            .Where(a => a.Platform == platform)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
            account.IsDefault = account.Id == accountId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SocialAccount?> ResolveForDownloadAsync(
        SocialPlatform platform,
        int? accountId,
        bool useDefaultWhenUnspecified = true,
        CancellationToken cancellationToken = default)
    {
        if (accountId is int id)
        {
            var account = await GetAccountByIdAsync(id, cancellationToken);
            if (account is null || account.Platform != platform)
                return null;

            return account.IsActive && SocialAccountAuth.HasAuth(account) ? account : null;
        }

        if (!useDefaultWhenUnspecified)
            return null;

        return await GetDefaultAccountAsync(platform, cancellationToken);
    }
}
