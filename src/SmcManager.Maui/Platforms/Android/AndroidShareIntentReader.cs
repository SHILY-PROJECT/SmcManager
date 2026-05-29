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

        var fromExtra = intent.GetStringExtra(Intent.ExtraText);
        if (!string.IsNullOrWhiteSpace(fromExtra))
            return fromExtra;

        var clip = intent.ClipData;
        if (clip is null || clip.ItemCount == 0)
            return null;

        for (var i = 0; i < clip.ItemCount; i++)
        {
            var item = clip.GetItemAt(i);
            if (item is null)
                continue;

            var text = item.Text;
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            var html = item.HtmlText?.ToString();
            if (!string.IsNullOrWhiteSpace(html))
                return html;
        }

        return null;
    }
}
