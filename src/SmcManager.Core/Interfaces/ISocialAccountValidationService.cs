using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Проверка, что cookies действительно дают авторизованную сессию.
/// </summary>
public interface ISocialAccountValidationService
{
    Task WarmupAsync(CancellationToken cancellationToken = default);

    Task<SocialAccountValidationResult> ValidateAsync(
        SocialPlatform platform,
        string? cookies,
        string? webPageUrl = null,
        CancellationToken cancellationToken = default);
}
