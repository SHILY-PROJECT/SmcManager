using Android.Content;

namespace SmcManager.Maui;

/// <summary>
/// Чтение текста из Android Share intent (Instagram часто кладёт URL в ClipData).
/// </summary>
internal static class AndroidShareIntentReader
{
    public static string? ReadText(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend)
            return null;

        var parts = new List<string>();

        var fromExtra = intent.GetStringExtra(Intent.ExtraText);
        if (!string.IsNullOrWhiteSpace(fromExtra))
            parts.Add(fromExtra);

        var subject = intent.GetStringExtra(Intent.ExtraSubject);
        if (!string.IsNullOrWhiteSpace(subject))
            parts.Add(subject);

        var clip = intent.ClipData;
        if (clip is not null)
        {
            for (var i = 0; i < clip.ItemCount; i++)
            {
                var item = clip.GetItemAt(i);
                if (item is null)
                    continue;

                var text = item.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(text);

                var html = item.HtmlText?.ToString();
                if (!string.IsNullOrWhiteSpace(html))
                    parts.Add(html);

                var uri = item.Uri?.ToString();
                if (!string.IsNullOrWhiteSpace(uri))
                    parts.Add(uri);
            }
        }

        if (parts.Count == 0)
            return null;

        foreach (var part in parts.OrderByDescending(ScoreUrlLikelihood))
        {
            if (ScoreUrlLikelihood(part) > 0)
                return part;
        }

        return parts[0];
    }

    private static int ScoreUrlLikelihood(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var lower = text.ToLowerInvariant();
        var score = 0;

        if (lower.Contains("instagram.com", StringComparison.Ordinal)
            || lower.Contains("instagr.am", StringComparison.Ordinal)
            || lower.Contains("youtube.com", StringComparison.Ordinal)
            || lower.Contains("youtu.be", StringComparison.Ordinal)
            || lower.Contains("vk.com", StringComparison.Ordinal))
            score += 4;

        if (lower.Contains("https://", StringComparison.Ordinal)
            || lower.Contains("http://", StringComparison.Ordinal))
            score += 3;

        if (lower.Contains("href=", StringComparison.Ordinal))
            score += 2;

        return score;
    }
}
