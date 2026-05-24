namespace SmcManager.Core.Interfaces;

/// <summary>
/// Открытие URL в браузере или приложении соцсети.
/// </summary>
public interface ILinkLauncherService
{
    Task OpenSourceAsync(string url);
}
