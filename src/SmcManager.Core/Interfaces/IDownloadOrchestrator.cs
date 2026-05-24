using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Координирует выбор загрузчика, скачивание и сохранение в БД.
/// </summary>
public interface IDownloadOrchestrator
{
    Task<DownloadResult> DownloadAndSaveAsync(DownloadRequest request, CancellationToken cancellationToken = default);
}
