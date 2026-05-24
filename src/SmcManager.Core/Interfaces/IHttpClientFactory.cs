using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Создаёт HttpClient с учётом прокси и cookies аккаунта.
/// </summary>
public interface IAppHttpClientFactory
{
    HttpClient CreateClient(SocialAccount? account = null);
}
