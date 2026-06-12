using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Загрузчик ВКонтакте через yt-dlp (YoutubeDLSharp).
/// </summary>
public class VkDownloader : IContentDownloader
{
    private readonly YtdlpHostService _ytdlp;
    private readonly IContentRepository _repository;
    private readonly IMediaStorageService _storage;

    public VkDownloader(
        YtdlpHostService ytdlp,
        IContentRepository repository,
        IMediaStorageService storage)
    {
        _ytdlp = ytdlp;
        _repository = repository;
        _storage = storage;
    }

    public SocialPlatform Platform => SocialPlatform.Vkontakte;

    public bool CanHandle(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        return host.Contains("vk.com") || host.Contains("vkontakte") || host.Contains("vk.ru");
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        if (!VkUrlParser.TryParse(request.Url, out var ownerId, out var itemId, out var kind))
        {
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = "Не удалось распознать ссылку ВКонтакте."
            };
        }

        var shortCode = VkUrlParser.BuildShortCode(ownerId, itemId);
        return await YtdlpDownloadHelper.DownloadAsync(
            _ytdlp,
            request,
            SocialPlatform.Vkontakte,
            kind,
            shortCode,
            _repository,
            _storage,
            cancellationToken);
    }
}
