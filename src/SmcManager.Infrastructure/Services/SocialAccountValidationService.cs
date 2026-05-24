using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Проверка формата cookies и живой сессии перед сохранением аккаунта.
/// </summary>
public sealed class SocialAccountValidationService : ISocialAccountValidationService
{
    private readonly YtdlpHostService _ytdlp;

    public SocialAccountValidationService(YtdlpHostService ytdlp) => _ytdlp = ytdlp;

    public Task WarmupAsync(CancellationToken cancellationToken = default) =>
        _ytdlp.WarmupAsync(cancellationToken);

    public async Task<SocialAccountValidationResult> ValidateAsync(
        SocialPlatform platform,
        string? cookies,
        string? webPageUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cookies))
        {
            return SocialAccountValidationResult.Ok(
                "Аккаунт без cookies — доступен только публичный контент.");
        }

        if (!SocialAccountAuth.ValidateAuth(platform, cookies, out var formatError))
        {
            return SocialAccountValidationResult.Fail(
                formatError ?? "Некорректные cookies для выбранной сети.");
        }

        var normalized = SocialAccountAuth.NormalizeAuthInput(platform, cookies);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(40));

        return await _ytdlp.ValidateSessionAsync(platform, normalized, webPageUrl, timeoutCts.Token)
            .ConfigureAwait(false);
    }
}
