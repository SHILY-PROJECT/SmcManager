using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Вход в соцсеть через встроенный браузер.
/// </summary>
public interface ISocialAuthService
{
    /// <summary>Открывает страницу входа и возвращает cookies после подтверждения пользователем.</summary>
    Task<SocialAuthResult?> LoginAsync(SocialPlatform platform, CancellationToken cancellationToken = default);
}
