using Microsoft.Extensions.Logging;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Выбирает загрузчик по URL, скачивает и сохраняет результат.
/// </summary>
public class DownloadOrchestrator : IDownloadOrchestrator
{
    private readonly IEnumerable<IContentDownloader> _downloaders;
    private readonly IContentRepository _repository;
    private readonly ILogger<DownloadOrchestrator> _logger;

    public DownloadOrchestrator(
        IEnumerable<IContentDownloader> downloaders,
        IContentRepository repository,
        ILogger<DownloadOrchestrator> logger)
    {
        _downloaders = downloaders;
        _repository = repository;
        _logger = logger;
    }

    public async Task<DownloadResult> DownloadAndSaveAsync(
        DownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Url = ContentUrlNormalizer.Normalize(request.Url);
        _logger.LogInformation(
            "DownloadAndSaveAsync: url={Url}, platform accountId={AccountId}, useAccount={UseAccount}, tagIds={TagIds}",
            request.Url,
            request.SocialAccountId,
            request.UseSocialAccount,
            request.TagIds);

        if (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("DownloadAndSaveAsync: invalid URL {Url}", request.Url);
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = "Некорректная ссылка."
            };
        }

        var downloader = _downloaders.FirstOrDefault(d => d.CanHandle(uri));
        if (downloader is null)
        {
            _logger.LogWarning("DownloadAndSaveAsync: no downloader for host {Host}", uri.Host);
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = "Платформа не поддерживается. Доступны Instagram, YouTube и ВКонтакте."
            };
        }

        _logger.LogDebug("DownloadAndSaveAsync: using {Downloader}", downloader.GetType().Name);

        await _repository.InitializeAsync(cancellationToken);
        var result = await downloader.DownloadAsync(request, cancellationToken);

        _logger.LogInformation(
            "DownloadAndSaveAsync: success={Success}, contentId={ContentId}, mediaCount={MediaCount}, error={Error}",
            result.Success,
            result.Content?.Id,
            result.Content?.MediaFiles.Count ?? 0,
            result.ErrorMessage);

        return result;
    }
}
