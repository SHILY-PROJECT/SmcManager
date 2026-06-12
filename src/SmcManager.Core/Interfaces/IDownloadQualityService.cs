using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Список доступных вариантов качества для URL.
/// </summary>
public interface IDownloadQualityService
{
    Task<IReadOnlyList<DownloadQualityOption>> GetQualitiesAsync(
        string url,
        int? socialAccountId,
        bool useSocialAccount,
        CancellationToken cancellationToken = default);
}
