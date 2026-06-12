using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Services;

/// <summary>
/// Нормализация и применение cookies / session для соцсетей.
/// </summary>
public static class SocialAccountAuth
{
    public static string? GetAuthData(SocialAccount account) =>
        !string.IsNullOrWhiteSpace(account.Cookies)
            ? account.Cookies.Trim()
            : account.SessionToken?.Trim();

    public static bool HasAuth(SocialAccount account) => !string.IsNullOrWhiteSpace(GetAuthData(account));

    public static string GetAuthMethodLabel(SocialAccount account) => account.AuthMethod switch
    {
        SocialAuthMethod.WebLogin when HasAuth(account) => "вход через браузер",
        SocialAuthMethod.ManualCookies when HasAuth(account) => "cookies вручную",
        _ => HasAuth(account) ? "авторизован" : "без авторизации"
    };

    /// <summary>Пытается извлечь @username из строки cookies.</summary>
    public static string? TryParseUsernameFromCookies(SocialPlatform platform, string cookieHeader)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader)) return null;

        var keys = ParseCookiePairs(cookieHeader);

        return platform switch
        {
            SocialPlatform.Instagram => keys.TryGetValue("ds_user_name", out var igUser) ? igUser : null,
            SocialPlatform.Vkontakte => null,
            _ => null
        };
    }

    /// <summary>Пытается извлечь username из URL страницы после входа в WebView.</summary>
    public static string? TryParseUsernameFromPageUrl(SocialPlatform platform, string? pageUrl)
    {
        if (string.IsNullOrWhiteSpace(pageUrl)) return null;

        return platform switch
        {
            SocialPlatform.Instagram => TryParseInstagramProfileSegment(pageUrl),
            _ => null
        };
    }

    /// <summary>Объединяет источники username (API, cookies, URL).</summary>
    public static string? ResolveUsername(
        SocialPlatform platform,
        string? cookieHeader,
        string? pageUrl = null,
        string? apiUsername = null)
    {
        foreach (var candidate in new[]
                 {
                     apiUsername,
                     TryParseUsernameFromCookies(platform, cookieHeader ?? string.Empty),
                     TryParseUsernameFromPageUrl(platform, pageUrl)
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim().TrimStart('@');
        }

        return null;
    }

    public static bool IsGenericDisplayName(string? displayName, SocialPlatform platform) =>
        !string.IsNullOrWhiteSpace(displayName)
        && displayName.Trim().Equals($"Аккаунт {GetPlatformTitle(platform)}", StringComparison.Ordinal);

    /// <summary>Краткая подпись аккаунта для списков и бейджей.</summary>
    public static string GetAccountShortLabel(SocialAccount account)
    {
        if (!string.IsNullOrWhiteSpace(account.DisplayName)
            && !IsGenericDisplayName(account.DisplayName, account.Platform))
        {
            return account.DisplayName.Trim();
        }

        var username = !string.IsNullOrWhiteSpace(account.Username)
            ? account.Username
            : TryParseUsernameFromCookies(account.Platform, GetAuthData(account) ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(username))
            return $"@{username.Trim().TrimStart('@')}";

        return HasAuth(account)
            ? $"{GetPlatformTitle(account.Platform)} (вход выполнен)"
            : GetPlatformTitle(account.Platform);
    }

    public static string? TryParseUsernameFromInstagramApiBody(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var match = Regex.Match(
            json,
            @"""username""\s*:\s*""([^""]+)""",
            RegexOptions.CultureInvariant);

        if (!match.Success) return null;

        var username = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(username) ? null : username;
    }

    private static string? TryParseInstagramProfileSegment(string pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0) return null;

        var first = segments[0];
        if (first.Equals("accounts", StringComparison.OrdinalIgnoreCase)
            || first.Equals("p", StringComparison.OrdinalIgnoreCase)
            || first.Equals("reel", StringComparison.OrdinalIgnoreCase)
            || first.Equals("reels", StringComparison.OrdinalIgnoreCase)
            || first.Equals("stories", StringComparison.OrdinalIgnoreCase)
            || first.Equals("explore", StringComparison.OrdinalIgnoreCase)
            || first.Equals("direct", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return first;
    }

    public static string BuildCookieHeader(SocialAccount account)
    {
        var raw = GetAuthData(account);
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        return account.Platform switch
        {
            SocialPlatform.Instagram => NormalizeInstagramAuth(raw),
            SocialPlatform.YouTube => NormalizeCookieHeader(raw),
            SocialPlatform.Vkontakte => NormalizeCookieHeader(raw),
            _ => NormalizeCookieHeader(raw)
        };
    }

    public static string NormalizeAuthInput(SocialPlatform platform, string input)
    {
        var trimmed = input.Trim();
        return platform switch
        {
            SocialPlatform.Instagram => NormalizeInstagramAuth(trimmed),
            _ => NormalizeCookieHeader(trimmed)
        };
    }

    public static string GetPlatformTitle(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram => "Instagram",
        SocialPlatform.YouTube => "YouTube",
        SocialPlatform.Vkontakte => "ВКонтакте",
        _ => platform.ToString()
    };

    public static string GetAuthHint(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram =>
            "Для закрытых постов и сторис: в браузере откройте instagram.com → F12 → Application → Cookies → скопируйте sessionid или всю строку Cookie.",
        SocialPlatform.YouTube =>
            "Для возрастных, приватных и member-only роликов: войдите на youtube.com → F12 → Network → любой запрос → Request Headers → скопируйте значение Cookie целиком.",
        SocialPlatform.Vkontakte =>
            "Для закрытых записей и видео: войдите на vk.com → F12 → Application → Cookies → скопируйте remixsid и remixstlid или всю строку Cookie из заголовка запроса.",
        _ => "Вставьте cookies из браузера после входа в аккаунт."
    };

    public static string GetAuthPlaceholder(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram => "sessionid=... или только значение sessionid",
        SocialPlatform.YouTube => "VISITOR_INFO1_LIVE=...; SAPISID=...; ...",
        SocialPlatform.Vkontakte => "remixsid=...; remixstlid=...; ...",
        _ => "Cookies из браузера"
    };

    /// <summary>Достаточно ли cookies для сессии Instagram (после входа в WebView).</summary>
    public static bool HasStrongInstagramSession(string cookieHeader, string? pageUrl = null)
    {
        var pairs = ParseCookiePairs(NormalizeCookieHeader(cookieHeader));
        if (!pairs.ContainsKey("sessionid")) return false;

        if (!string.IsNullOrWhiteSpace(pageUrl)
            && pageUrl.Contains("/accounts/login", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return pairs.ContainsKey("csrftoken")
               || pairs.ContainsKey("ds_user_id")
               || pairs.ContainsKey("ds_user_name");
    }

    /// <summary>Заголовки для запросов к API Instagram.</summary>
    public static void ApplyInstagramApiHeaders(HttpRequestHeaders headers, string cookieHeader)
    {
        var normalized = NormalizeCookieHeader(cookieHeader);
        var pairs = ParseCookiePairs(normalized);

        headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        headers.TryAddWithoutValidation("Cookie", normalized);
        headers.TryAddWithoutValidation("X-IG-App-ID", "936619743392459");
        headers.TryAddWithoutValidation("X-ASBD-ID", "129477");
        headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        headers.TryAddWithoutValidation("X-Instagram-AJAX", "1");
        headers.TryAddWithoutValidation("Referer", "https://www.instagram.com/");
        headers.TryAddWithoutValidation("Origin", "https://www.instagram.com");

        if (pairs.TryGetValue("csrftoken", out var csrf) && !string.IsNullOrWhiteSpace(csrf))
            headers.TryAddWithoutValidation("X-CSRFToken", csrf);
    }

    public static bool ValidateAuth(SocialPlatform platform, string authData, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(authData))
        {
            warning = "Без cookies скачивается только публичный контент.";
            return false;
        }

        var normalized = NormalizeAuthInput(platform, authData);
        var keys = ParseCookieKeys(normalized);

        switch (platform)
        {
            case SocialPlatform.Instagram:
                if (!keys.Contains("sessionid"))
                {
                    warning = "Не найден sessionid — проверьте, что скопировали cookie Instagram.";
                    return false;
                }
                return true;

            case SocialPlatform.YouTube:
                if (!keys.Contains("SAPISID") && !keys.Contains("SID") && !keys.Contains("LOGIN_INFO"))
                {
                    warning = "Похоже, cookies YouTube неполные. Нужна строка Cookie из запроса к youtube.com после входа.";
                    return false;
                }
                return true;

            case SocialPlatform.Vkontakte:
                if (!keys.Contains("remixsid"))
                {
                    warning = "Не найден remixsid — войдите на vk.com и скопируйте cookies снова.";
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    private static string NormalizeInstagramAuth(string input)
    {
        if (input.Contains(';') || input.Contains('='))
            return NormalizeCookieHeader(input);

        return $"sessionid={input.Trim()}";
    }

    private static string NormalizeCookieHeader(string input) =>
        input.Replace("\r\n", "; ", StringComparison.Ordinal)
            .Replace('\n', ';')
            .Replace(";;", ";", StringComparison.Ordinal)
            .Trim().TrimEnd(';');

    private static HashSet<string> ParseCookieKeys(string cookieHeader)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in ParseCookiePairs(cookieHeader).Keys)
            keys.Add(key);
        return keys;
    }

    private static Dictionary<string, string> ParseCookiePairs(string cookieHeader)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq > 0)
                pairs[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }

        return pairs;
    }
}
