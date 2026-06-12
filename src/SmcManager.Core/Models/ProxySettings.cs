namespace SmcManager.Core.Models;

/// <summary>
/// Настройки HTTP/SOCKS прокси для запросов к соцсетям.
/// </summary>
public class ProxySettings
{
    public bool IsEnabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 8080;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; }
}
