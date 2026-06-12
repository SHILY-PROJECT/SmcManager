using Microsoft.Extensions.Logging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Download;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

using System.Text;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Обёртка над yt-dlp (YoutubeDLSharp): инициализация, метаданные и скачивание.
/// </summary>
public sealed class YtdlpHostService
{
    private readonly ISocialAccountService _accountService;
    private readonly ILogger<YtdlpHostService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private YoutubeDL? _client;
    private string _toolsDirectory = string.Empty;

    public YtdlpHostService(ISocialAccountService accountService, ILogger<YtdlpHostService> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    public Task WarmupAsync(CancellationToken cancellationToken) =>
        Task.Run(async () => await EnsureReadyAsync(cancellationToken).ConfigureAwait(false), cancellationToken);

    public async Task<LinkMetadataResult> GetLinkMetadataAsync(
        string url,
        SocialPlatform platform,
        int? accountId,
        bool useSocialAccount,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "GetLinkMetadataAsync: url={Url}, platform={Platform}, accountId={AccountId}, ytdlpAvailable={Available}",
            url,
            platform,
            accountId,
            YtdlpRuntimeSupport.IsAvailable);

        if (!YtdlpRuntimeSupport.IsAvailable)
        {
            _logger.LogWarning(
                "GetLinkMetadataAsync: yt-dlp недоступен на {OS}. Превью не будет загружено. {Hint}",
                Environment.OSVersion,
                YtdlpRuntimeSupport.MobileUnsupportedMessage);
            return new LinkMetadataResult
            {
                Qualities = [DownloadQualityOption.BestQuality(platform)]
            };
        }

        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        if (_client is null)
        {
            _logger.LogError("GetLinkMetadataAsync: yt-dlp client is null after EnsureReadyAsync");
            return new LinkMetadataResult
            {
                Qualities = [DownloadQualityOption.BestQuality(platform)]
            };
        }

        var account = await ResolveAccountAsync(platform, accountId, useSocialAccount, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogDebug(
            "GetLinkMetadataAsync: account resolved id={AccountId}, hasCookies={HasCookies}",
            account?.Id,
            !string.IsNullOrWhiteSpace(account?.Cookies));

        string? cookieFile = null;
        try
        {
            cookieFile = await YtdlpCookieHelper.WriteCookieFileAsync(account, cancellationToken)
                .ConfigureAwait(false);

            var fetch = await RunFetchOnBackgroundAsync(url, cookieFile, platform, cancellationToken)
                .ConfigureAwait(false);

            if (!fetch.Success || fetch.Data is not VideoData video)
            {
                var log = BuildLogText(fetch.ErrorOutput, null);
                _logger.LogWarning(
                    "GetLinkMetadataAsync: fetch failed. Success={Success}, HasData={HasData}, Log={Log}",
                    fetch.Success,
                    fetch.Data is VideoData,
                    Truncate(log, 800));
                return new LinkMetadataResult
                {
                    Qualities = [DownloadQualityOption.BestQuality(platform)]
                };
            }

            var formats = ResolveFormats(video);
            var preview = YtdlpPreviewMapper.FromVideoData(video, platform, url);
            _logger.LogInformation(
                "GetLinkMetadataAsync: fetch ok. Title={Title}, Author={Author}, Thumb={Thumb}, Formats={FormatCount}",
                preview?.Title,
                preview?.Author,
                preview?.ThumbnailUrl,
                formats?.Length ?? 0);

            return new LinkMetadataResult
            {
                Preview = preview,
                Qualities = YtdlpQualityBuilder.FromFormats(formats, platform)
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("GetLinkMetadataAsync cancelled for {Url}", url);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLinkMetadataAsync exception for {Url}", url);
            return new LinkMetadataResult
            {
                Qualities = [DownloadQualityOption.BestQuality(platform)]
            };
        }
        finally
        {
            YtdlpCookieHelper.TryDelete(cookieFile);
        }
    }

    public async Task<SocialAccountValidationResult> ValidateSessionAsync(
        SocialPlatform platform,
        string normalizedCookies,
        string? webPageUrl,
        CancellationToken cancellationToken)
    {
        if (platform == SocialPlatform.Instagram)
        {
            return await TryValidateInstagramSessionAsync(normalizedCookies, webPageUrl, cancellationToken)
                .ConfigureAwait(false);
        }

        var probeUrl = GetSessionProbeUrl(platform);
        var account = new SocialAccount { Platform = platform, Cookies = normalizedCookies };
        string? cookieFile = null;

        try
        {
            await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
            cookieFile = await YtdlpCookieHelper.WriteCookieFileAsync(account, cancellationToken)
                .ConfigureAwait(false);

            var fetch = await RunFetchOnBackgroundAsync(probeUrl, cookieFile, platform, cancellationToken)
                .ConfigureAwait(false);

            var log = BuildLogText(fetch.ErrorOutput, null);

            if (!fetch.Success)
            {
                return SocialAccountValidationResult.Fail(
                    DescribeSessionFailure(platform, log, null));
            }

            if (fetch.Data is not VideoData video)
            {
                return SocialAccountValidationResult.Fail(
                    "Не удалось проверить сессию. Попробуйте войти через браузер ещё раз.");
            }

            if (LooksLikeLoginRequired(video, log, platform))
            {
                return SocialAccountValidationResult.Fail(
                    "Вход не подтверждён: cookies устарели или скопированы без авторизации.");
            }

            var username = SocialAccountAuth.TryParseUsernameFromCookies(platform, normalizedCookies)
                           ?? video.UploaderID
                           ?? video.Uploader
                           ?? video.Channel;

            username = username?.Trim().TrimStart('@');
            if (string.IsNullOrWhiteSpace(username)
                || username.Equals("instagram", StringComparison.OrdinalIgnoreCase)
                || username.Equals("youtube", StringComparison.OrdinalIgnoreCase))
            {
                username = SocialAccountAuth.TryParseUsernameFromCookies(platform, normalizedCookies);
            }

            return SocialAccountValidationResult.Ok(
                $"Авторизация в {SocialAccountAuth.GetPlatformTitle(platform)} подтверждена.",
                username);
        }
        catch (OperationCanceledException)
        {
            return SocialAccountValidationResult.Fail(
                "Проверка авторизации прервана по таймауту. Проверьте интернет и попробуйте снова.");
        }
        catch (Exception ex)
        {
            return SocialAccountValidationResult.Fail(
                $"Ошибка проверки сессии: {ex.Message}");
        }
        finally
        {
            YtdlpCookieHelper.TryDelete(cookieFile);
        }
    }

    private static async Task<SocialAccountValidationResult> TryValidateInstagramSessionAsync(
        string normalizedCookies,
        string? webPageUrl,
        CancellationToken cancellationToken)
    {
        var username = SocialAccountAuth.TryParseUsernameFromCookies(
            SocialPlatform.Instagram, normalizedCookies);

        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };

            SocialAccountAuth.ApplyInstagramApiHeaders(client.DefaultRequestHeaders, normalizedCookies);

            var apiUrls = new[]
            {
                "https://www.instagram.com/api/v1/accounts/current_user/?edit=true",
                "https://www.instagram.com/api/v1/web/accounts/current_user/"
            };

            foreach (var apiUrl in apiUrls)
            {
                using var response = await client.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (body.Contains("\"status\":\"fail\"", StringComparison.OrdinalIgnoreCase)
                    || body.Contains("login_required", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                username = SocialAccountAuth.ResolveUsername(
                    SocialPlatform.Instagram,
                    normalizedCookies,
                    webPageUrl,
                    SocialAccountAuth.TryParseUsernameFromInstagramApiBody(body));

                return SocialAccountValidationResult.Ok(
                    "Авторизация Instagram подтверждена.",
                    username);
            }

            if (SocialAccountAuth.HasStrongInstagramSession(normalizedCookies, webPageUrl))
            {
                username = SocialAccountAuth.ResolveUsername(
                    SocialPlatform.Instagram, normalizedCookies, webPageUrl, username);

                return SocialAccountValidationResult.Ok(
                    "Сессия Instagram принята из браузера (cookies подтверждены).",
                    username);
            }

            return SocialAccountValidationResult.Fail(
                "Вход в Instagram не подтверждён. Откройте ленту instagram.com в окне выше "
                + "(не страницу входа) и нажмите «Готово» снова.");
        }
        catch (OperationCanceledException)
        {
            return SocialAccountValidationResult.Fail(
                "Проверка Instagram прервана по таймауту. Проверьте интернет и попробуйте снова.");
        }
        catch (Exception ex)
        {
            if (SocialAccountAuth.HasStrongInstagramSession(normalizedCookies, webPageUrl))
            {
                username = SocialAccountAuth.ResolveUsername(
                    SocialPlatform.Instagram, normalizedCookies, webPageUrl, username);

                return SocialAccountValidationResult.Ok(
                    "Сессия Instagram принята из браузера.",
                    username);
            }

            return SocialAccountValidationResult.Fail(
                $"Не удалось проверить Instagram: {ex.Message}");
        }
    }

    private static string GetSessionProbeUrl(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram => "https://www.instagram.com/accounts/edit/",
        SocialPlatform.YouTube => "https://www.youtube.com/feed/you",
        SocialPlatform.Vkontakte => "https://vk.com/feed",
        _ => "https://www.google.com"
    };

    private static bool LooksLikeLoginRequired(VideoData video, string log, SocialPlatform platform)
    {
        if (ContainsLoginHint(log)) return true;

        var title = video.Title ?? string.Empty;
        var description = video.Description ?? string.Empty;
        var id = video.ID ?? string.Empty;
        var uploader = video.Uploader ?? string.Empty;

        if (ContainsLoginHint(title) || ContainsLoginHint(description)) return true;

        if (platform == SocialPlatform.Instagram
            && (id.Contains("login", StringComparison.OrdinalIgnoreCase)
                || uploader.Contains("login", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsLoginHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        return text.Contains("login required", StringComparison.OrdinalIgnoreCase)
               || text.Contains("sign in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("please log in", StringComparison.OrdinalIgnoreCase)
               || text.Contains("cookies", StringComparison.OrdinalIgnoreCase)
                  && text.Contains("invalid", StringComparison.OrdinalIgnoreCase)
               || text.Contains("войдите", StringComparison.OrdinalIgnoreCase)
               || text.Contains("authentication", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeSessionFailure(
        SocialPlatform platform,
        string log,
        VideoData? video)
    {
        if (ContainsLoginHint(log))
        {
            return $"Сессия {SocialAccountAuth.GetPlatformTitle(platform)} недействительна. "
                   + "Войдите снова через браузер или обновите cookies.";
        }

        return $"Не удалось подтвердить вход в {SocialAccountAuth.GetPlatformTitle(platform)}. "
               + "Проверьте cookies и попробуйте снова.";
    }

    private Task<YoutubeDLSharp.RunResult<VideoData>> RunFetchOnBackgroundAsync(
        string url,
        string? cookieFile,
        SocialPlatform platform,
        CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
            return await _client!.RunVideoDataFetch(
                url,
                cancellationToken,
                overrideOptions: BuildFetchOptions(cookieFile, platform)).ConfigureAwait(false);
        }, cancellationToken);

    public async Task<IReadOnlyList<DownloadQualityOption>> GetQualitiesAsync(
        string url,
        SocialPlatform platform,
        int? accountId,
        bool useSocialAccount,
        CancellationToken cancellationToken)
    {
        url = ContentUrlNormalizer.Normalize(url);
        await EnsureReadyAsync(cancellationToken);

        var account = await ResolveAccountAsync(platform, accountId, useSocialAccount, cancellationToken);
        string? cookieFile = null;
        try
        {
            cookieFile = await YtdlpCookieHelper.WriteCookieFileAsync(account, cancellationToken);
            var fetch = await _client!.RunVideoDataFetch(
                url,
                cancellationToken,
                overrideOptions: BuildFetchOptions(cookieFile, platform));

            if (!fetch.Success || fetch.Data is not VideoData video)
                return [DownloadQualityOption.BestQuality(platform)];

            var formats = ResolveFormats(video);
            return YtdlpQualityBuilder.FromFormats(formats, platform);
        }
        catch
        {
            return [DownloadQualityOption.BestQuality(platform)];
        }
        finally
        {
            YtdlpCookieHelper.TryDelete(cookieFile);
        }
    }

    public async Task<YtdlpDownloadResult> DownloadAsync(
        DownloadRequest request,
        SocialPlatform platform,
        CancellationToken cancellationToken)
    {
        if (!YtdlpRuntimeSupport.IsAvailable)
        {
            return new YtdlpDownloadResult(
                false,
                null,
                YtdlpRuntimeSupport.MobileUnsupportedMessage);
        }

        var downloadUrl = ContentUrlNormalizer.Normalize(request.Url);
        await EnsureReadyAsync(cancellationToken);

        if (_client is null)
        {
            return new YtdlpDownloadResult(
                false,
                null,
                "Не удалось инициализировать yt-dlp.");
        }

        var account = await ResolveAccountAsync(
            platform,
            request.SocialAccountId,
            request.UseSocialAccount,
            cancellationToken);

        var outputDir = Path.Combine(Path.GetTempPath(), "smc-ytdlp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        var downloadStarted = DateTime.UtcNow;

        string? cookieFile = null;
        try
        {
            cookieFile = await YtdlpCookieHelper.WriteCookieFileAsync(account, cancellationToken);
            var isVideoContent = YtdlpQualityBuilder.IsVideoContent(platform, request.ContentKind);
            var format = YtdlpQualityBuilder.ResolveFormatSelector(
                request.QualityFormatId,
                request.QualityFormatSelector,
                platform,
                request.ContentKind);
            var mergeFormat = isVideoContent
                ? DownloadMergeFormat.Mp4
                : platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte
                    ? DownloadMergeFormat.Unspecified
                    : DownloadMergeFormat.Mp4;

            var options = BuildDownloadOptions(outputDir, cookieFile, platform);
            var previousOutput = _client!.OutputFolder;
            _client.OutputFolder = outputDir;

            try
            {
                var fetch = await _client.RunVideoDataFetch(
                    downloadUrl,
                    cancellationToken,
                    overrideOptions: BuildFetchOptions(cookieFile, platform));

                var video = fetch.Data as VideoData;

                string? authorUsername = null;
                if (platform == SocialPlatform.Instagram)
                {
                    authorUsername = await InstagramAuthorResolver.ResolveAsync(
                        downloadUrl, video, account, cancellationToken);
                }

                var result = await _client.RunVideoDownload(
                    downloadUrl,
                    format,
                    mergeFormat,
                    VideoRecodeFormat.None,
                    cancellationToken,
                    overrideOptions: options);

                var logText = BuildLogText(result.ErrorOutput, result.Data as string);

                if (!result.Success && platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte)
                {
                    if (!isVideoContent && YtdlpQualityBuilder.ShouldRetryWithAllFormats(format, logText))
                    {
                        result = await _client.RunVideoDownload(
                            downloadUrl,
                            "all",
                            mergeFormat,
                            VideoRecodeFormat.None,
                            cancellationToken,
                            overrideOptions: options);
                        logText = BuildLogText(result.ErrorOutput, result.Data as string);
                        format = "all";
                    }

                    if (!result.Success
                        && !isVideoContent
                        && YtdlpQualityBuilder.ShouldRetryInstagramMedia(format, logText))
                    {
                        result = await _client.RunVideoDownload(
                            downloadUrl,
                            YtdlpQualityBuilder.PhotoFriendlyFormatSelector,
                            mergeFormat,
                            VideoRecodeFormat.None,
                            cancellationToken,
                            overrideOptions: options);
                        logText = BuildLogText(result.ErrorOutput, result.Data as string);
                        format = YtdlpQualityBuilder.PhotoFriendlyFormatSelector;
                    }

                    if (!result.Success
                        && (isVideoContent || YtdlpQualityBuilder.ShouldRetryWithPlainBest(format, logText)))
                    {
                        var retryFormat = isVideoContent
                            ? YtdlpQualityBuilder.VideoFormatSelector
                            : "best";
                        result = await _client.RunVideoDownload(
                            downloadUrl,
                            retryFormat,
                            mergeFormat,
                            VideoRecodeFormat.None,
                            cancellationToken,
                            overrideOptions: options);
                        logText = BuildLogText(result.ErrorOutput, result.Data as string);
                        format = retryFormat;
                    }
                }
                else if (!result.Success && YtdlpQualityBuilder.ShouldRetryWithPlainBest(format, logText))
                {
                    result = await _client.RunVideoDownload(
                        downloadUrl,
                        "best",
                        mergeFormat,
                        VideoRecodeFormat.None,
                        cancellationToken,
                        overrideOptions: options);
                    logText = BuildLogText(result.ErrorOutput, result.Data as string);
                }

                var files = await CollectDownloadedFilesAsync(
                    outputDir,
                    previousOutput,
                    downloadStarted,
                    result.Data as string,
                    isVideoContent,
                    cancellationToken);

                if (files.Count == 0 && platform == SocialPlatform.Instagram)
                {
                    var directFiles = await InstagramDirectMediaDownloader.TryDownloadAsync(
                        downloadUrl,
                        account,
                        outputDir,
                        video,
                        request.ContentKind,
                        cancellationToken);
                    if (directFiles.Count > 0)
                        files = directFiles.ToList();
                }

                if (isVideoContent)
                    files = FilterValidVideoFiles(files);

                if (files.Count > 0)
                    return new YtdlpDownloadResult(
                        true,
                        new YtdlpDownloadPayload(video, files, authorUsername),
                        null);

                if (!result.Success)
                {
                    var error = FormatDownloadError(result.ErrorOutput, platform);
                    return new YtdlpDownloadResult(false, null, error);
                }

                var message = BuildEmptyFilesMessage(platform, account, logText);
                return new YtdlpDownloadResult(false, null, message);
            }
            finally
            {
                _client.OutputFolder = previousOutput;
            }
        }
        finally
        {
            YtdlpCookieHelper.TryDelete(cookieFile);
        }
    }

    private static string FormatDownloadError(string[]? errors, SocialPlatform platform)
    {
        var raw = errors is { Length: > 0 }
            ? string.Join(Environment.NewLine, errors)
            : "yt-dlp не смог скачать файл.";

        if (raw.Contains("Downloading 0 items", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("downloading 0 items", StringComparison.OrdinalIgnoreCase))
        {
            return platform == SocialPlatform.Instagram
                ? "Instagram не отдал медиа (0 файлов). Сессия устарела или cookies неполные — "
                  + "в настройках снова войдите через браузер и нажмите «Готово» на открытой ленте."
                : "Контент недоступен без авторизации. Подключите аккаунт в настройках.";
        }

        if (raw.Contains("No video formats found", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("no video formats", StringComparison.OrdinalIgnoreCase))
        {
            return platform == SocialPlatform.Instagram
                ? "Не удалось сохранить медиа поста (yt-dlp и прямая загрузка). Проверьте интернет "
                  + "и доступность поста, затем попробуйте снова."
                : "Не найден подходящий формат. Попробуйте другое качество или аккаунт с авторизацией.";
        }

        var lines = raw.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 ? lines[^1] : raw;
    }

    private static string BuildLogText(string[]? errors, string? data)
    {
        var sb = new StringBuilder();
        if (errors is { Length: > 0 })
            sb.AppendLine(string.Join(Environment.NewLine, errors));
        if (!string.IsNullOrWhiteSpace(data))
            sb.AppendLine(data);
        return sb.ToString();
    }

    private static string BuildEmptyFilesMessage(
        SocialPlatform platform,
        SocialAccount? account,
        string logText)
    {
        if (platform == SocialPlatform.Instagram)
        {
            var needsAuth = logText.Contains("Downloading 0 items", StringComparison.OrdinalIgnoreCase)
                            || logText.Contains("0 items", StringComparison.OrdinalIgnoreCase);

            if (needsAuth && !SocialAccountAuth.HasAuth(account ?? new SocialAccount()))
            {
                return "Instagram не отдал медиа без входа. На вкладке «Скачать» выберите аккаунт "
                       + "или в настройках войдите через браузер / вставьте cookies.";
            }

            if (needsAuth)
            {
                return "Instagram не отдал файлы для этой ссылки. Обновите cookies аккаунта в настройках "
                       + "или проверьте, что пост доступен вашему аккаунту.";
            }
        }

        return platform == SocialPlatform.Instagram
            ? "Не удалось сохранить медиа поста. Для каруселей и закрытых постов выберите аккаунт "
              + "Instagram на вкладке «Скачать» или обновите cookies в настройках."
            : "Файлы не найдены после скачивания. Попробуйте другой аккаунт или качество.";
    }

    private static List<string> FilterValidVideoFiles(IReadOnlyList<string> files) =>
        files.Where(f => MediaFileValidator.IsValidFile(f, requireVideo: true)).ToList();

    private static async Task<List<string>> CollectDownloadedFilesAsync(
        string outputDir,
        string? fallbackDir,
        DateTime notBeforeUtc,
        string? resultPath,
        bool requireValidVideo,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(resultPath)
            && File.Exists(resultPath)
            && IsMediaFile(resultPath)
            && (!requireValidVideo || MediaFileValidator.IsValidFile(resultPath, requireVideo: true)))
        {
            return [resultPath];
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = FindMediaFiles(outputDir, notBeforeUtc);
            if (files.Count == 0 && !string.IsNullOrWhiteSpace(fallbackDir) && fallbackDir != outputDir)
                files = FindMediaFiles(fallbackDir, notBeforeUtc);

            if (files.Count > 0)
            {
                if (requireValidVideo)
                    files = FilterValidVideoFiles(files);

                if (files.Count > 0)
                    return files;
            }

            await Task.Delay(300, cancellationToken);
        }

        return [];
    }

    private static List<string> FindMediaFiles(string directory, DateTime notBeforeUtc)
    {
        if (!Directory.Exists(directory)) return [];

        return Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                        && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                        && !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        && IsMediaFile(f)
                        && File.GetLastWriteTimeUtc(f) >= notBeforeUtc.AddSeconds(-5))
            .OrderBy(f => f)
            .ToList();
    }

    private static bool IsMediaFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    public static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        try { Directory.Delete(path, recursive: true); } catch { /* ignore */ }
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (!YtdlpRuntimeSupport.IsAvailable)
        {
            _logger.LogDebug("EnsureReadyAsync: skipped, yt-dlp not available on this platform");
            return;
        }

        if (_client is not null) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null) return;

            _toolsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmcManager",
                "yt-dlp");
            Directory.CreateDirectory(_toolsDirectory);
            _logger.LogInformation("EnsureReadyAsync: downloading yt-dlp tools to {Dir}", _toolsDirectory);

            var previousDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_toolsDirectory);
                await YoutubeDLSharp.Utils.DownloadYtDlp();
                await YoutubeDLSharp.Utils.DownloadFFmpeg();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousDir);
            }

            var ytdlpPath = YoutubeDLSharp.Utils.GetFullPath(GetYtDlpBinaryName())
                            ?? Path.Combine(_toolsDirectory, GetYtDlpBinaryName());
            var ffmpegPath = YoutubeDLSharp.Utils.GetFullPath(GetFfmpegBinaryName())
                             ?? Path.Combine(_toolsDirectory, GetFfmpegBinaryName());

            _logger.LogInformation(
                "EnsureReadyAsync: ytdlp={YtdlpPath} exists={YtdlpExists}, ffmpeg={FfmpegPath} exists={FfmpegExists}",
                ytdlpPath,
                File.Exists(ytdlpPath),
                ffmpegPath,
                File.Exists(ffmpegPath));

            _client = new YoutubeDL
            {
                YoutubeDLPath = ytdlpPath,
                FFmpegPath = ffmpegPath,
                OutputFolder = _toolsDirectory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnsureReadyAsync failed");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text[..maxLength] + "…";
    }

    private static string GetYtDlpBinaryName() =>
        OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

    private static string GetFfmpegBinaryName() =>
        OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    private async Task<SocialAccount?> ResolveAccountAsync(
        SocialPlatform platform,
        int? accountId,
        bool useSocialAccount,
        CancellationToken cancellationToken)
    {
        return await _accountService.ResolveForDownloadAsync(
            platform, accountId, useSocialAccount, cancellationToken);
    }

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static OptionSet BuildFetchOptions(string? cookieFile, SocialPlatform platform)
    {
        var options = new OptionSet
        {
            NoWarnings = true,
            NoPlaylist = platform is SocialPlatform.YouTube,
            Cookies = cookieFile,
            UserAgent = BrowserUserAgent
        };

        if (platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte)
            options.IgnoreNoFormatsError = true;

        return options;
    }

    private static OptionSet BuildDownloadOptions(string outputDir, string? cookieFile, SocialPlatform platform)
    {
        var options = new OptionSet
        {
            NoWarnings = true,
            NoPlaylist = platform is SocialPlatform.YouTube,
            Cookies = cookieFile,
            Output = BuildOutputTemplate(outputDir),
            RestrictFilenames = true,
            UserAgent = BrowserUserAgent
        };

        if (platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte)
            options.IgnoreNoFormatsError = true;

        return options;
    }

    private static string BuildOutputTemplate(string outputDir)
    {
        var fullDir = Path.GetFullPath(outputDir);
        return Path.Combine(fullDir, "%(playlist_index|)s%(id)s.%(ext)s")
            .Replace('\\', '/');
    }

    private static FormatData[]? ResolveFormats(VideoData video)
    {
        if (video.Formats is { Length: > 0 })
            return video.Formats;

        return video.Entries?.FirstOrDefault(e => e.Formats is { Length: > 0 })?.Formats;
    }
}

public sealed record YtdlpDownloadResult(bool Success, YtdlpDownloadPayload? Payload, string? ErrorMessage);

public sealed record YtdlpDownloadPayload(
    VideoData? Video,
    IReadOnlyList<string> FilePaths,
    string? AuthorUsername = null);
