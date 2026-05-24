using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Метаданные и превью по ссылке (без скачивания файла).
/// </summary>
public interface ILinkMetadataService
{
    /// <summary>Предзагрузка yt-dlp в фоне при открытии вкладки.</summary>
    Task WarmupAsync(CancellationToken cancellationToken = default);

    Task<LinkMetadataResult> GetMetadataAsync(
        string url,
        int? socialAccountId,
        bool useSocialAccount,
        CancellationToken cancellationToken = default);
}
