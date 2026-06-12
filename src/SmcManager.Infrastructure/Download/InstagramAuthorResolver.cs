using System.Text.RegularExpressions;
using SmcManager.Core.Models;
using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Определяет @username автора Instagram (не числовой user id).
/// </summary>
internal static partial class InstagramAuthorResolver
{
    [GeneratedRegex(@"""username""\s*:\s*""([A-Za-z0-9._]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameJsonRegex();

    public static async Task<string?> ResolveAsync(
        string postUrl,
        VideoData? video,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in EnumerateCandidates(video))
        {
            if (IsLikelyUsername(candidate))
                return candidate;
        }

        if (account is not null)
        {
            var fromApi = await InstagramMediaApiFetcher.TryGetAuthorUsernameAsync(
                postUrl, account, cancellationToken).ConfigureAwait(false);
            if (IsLikelyUsername(fromApi))
                return fromApi;
        }

        foreach (var fetchUrl in BuildFetchUrls(postUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var html = await TryFetchHtmlAsync(fetchUrl, account, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(html)) continue;

            foreach (Match match in UsernameJsonRegex().Matches(html))
            {
                if (IsLikelyUsername(match.Groups[1].Value))
                    return match.Groups[1].Value;
            }
        }

        return null;
    }

    public static string? PickFromVideo(VideoData? video)
    {
        foreach (var candidate in EnumerateCandidates(video))
        {
            if (IsLikelyUsername(candidate))
                return candidate;
        }

        return null;
    }

    internal static bool IsLikelyUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var username = value.Trim().TrimStart('@');
        if (username.Length is < 1 or > 30)
            return false;

        if (username.All(char.IsDigit))
            return false;

        if (username.Equals("instagram", StringComparison.OrdinalIgnoreCase)
            || username.Equals("reels", StringComparison.OrdinalIgnoreCase)
            || username.Equals("p", StringComparison.OrdinalIgnoreCase))
            return false;

        return username.All(c => char.IsLetterOrDigit(c) || c is '_' or '.');
    }

    private static IEnumerable<string?> EnumerateCandidates(VideoData? video)
    {
        video = ResolvePrimary(video);
        if (video is null) yield break;

        yield return video.Uploader;
        yield return video.Channel;
        yield return video.Creator;

        if (!string.IsNullOrWhiteSpace(video.UploaderID) && !video.UploaderID.All(char.IsDigit))
            yield return video.UploaderID;

        if (!string.IsNullOrWhiteSpace(video.ChannelID) && !video.ChannelID.All(char.IsDigit))
            yield return video.ChannelID;
    }

    private static VideoData? ResolvePrimary(VideoData? video)
    {
        if (video is null) return null;
        if (video.Entries is not { Length: > 0 }) return video;
        return video.Entries[0];
    }

    private static IEnumerable<string> BuildFetchUrls(string postUrl)
    {
        var normalized = postUrl.TrimEnd('/');
        yield return normalized.EndsWith("/embed/captioned/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed/captioned/";
        yield return normalized;
    }

    private static async Task<string?> TryFetchHtmlAsync(
        string url,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = InstagramMediaApiFetcher.CreateHttpClient(account);
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
