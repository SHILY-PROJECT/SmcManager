using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Загрузчик контента для конкретной соцсети.
/// </summary>
public interface IContentDownloader
{
    SocialPlatform Platform { get; }

    bool CanHandle(Uri url);

    Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default);
}
