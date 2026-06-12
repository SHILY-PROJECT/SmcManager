using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Управление аккаунтами соцсетей и выбором сессии для скачивания.
/// </summary>
public interface ISocialAccountService
{
    Task<IReadOnlyList<SocialAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialAccount>> GetAccountsForPlatformAsync(
        SocialPlatform platform,
        CancellationToken cancellationToken = default);

    Task<SocialAccount?> GetAccountByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<SocialAccount?> GetDefaultAccountAsync(
        SocialPlatform platform,
        CancellationToken cancellationToken = default);

    Task<SocialAccount> SaveAccountAsync(SocialAccount account, CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(int accountId, CancellationToken cancellationToken = default);

    Task SetDefaultAccountAsync(
        SocialPlatform platform,
        int accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Явный id → аккаунт; иначе аккаунт по умолчанию для платформы.
    /// </summary>
    /// <param name="useDefaultWhenUnspecified">false — не подставлять аккаунт по умолчанию (режим «без аккаунта»).</param>
    Task<SocialAccount?> ResolveForDownloadAsync(
        SocialPlatform platform,
        int? accountId,
        bool useDefaultWhenUnspecified = true,
        CancellationToken cancellationToken = default);
}
