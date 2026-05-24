using System.Text.RegularExpressions;
using SmcManager.Core.Enums;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Парсинг Instagram URL: shortcode, тип контента, username для stories.
/// </summary>
public static partial class InstagramUrlParser
{
    public static bool TryParse(string url, out string? shortCode, out ContentKind kind, out string? storyUsername)
    {
        shortCode = null;
        storyUsername = null;
        kind = ContentKind.Post;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var path = uri.AbsolutePath.Trim('/');

        var storyMatch = StoryRegex().Match(path);
        if (storyMatch.Success)
        {
            kind = ContentKind.Story;
            storyUsername = storyMatch.Groups["user"].Value;
            shortCode = storyMatch.Groups["id"].Value;
            return true;
        }

        var reelMatch = ReelRegex().Match(path);
        if (reelMatch.Success)
        {
            kind = ContentKind.Reel;
            shortCode = reelMatch.Groups["code"].Value;
            return true;
        }

        var postMatch = PostRegex().Match(path);
        if (postMatch.Success)
        {
            kind = ContentKind.Post;
            shortCode = postMatch.Groups["code"].Value;
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"stories/(?<user>[^/]+)/(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex StoryRegex();

    [GeneratedRegex(@"(?:reel|reels)/(?<code>[^/?#]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ReelRegex();

    [GeneratedRegex(@"p/(?<code>[^/?#]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PostRegex();
}
