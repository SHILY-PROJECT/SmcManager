namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Преобразование shortcode поста Instagram в числовой media_pk для API.
/// </summary>
internal static class InstagramShortcodeConverter
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    public static bool TryToMediaPk(string shortcode, out long mediaPk)
    {
        mediaPk = 0;
        if (string.IsNullOrWhiteSpace(shortcode))
            return false;

        foreach (var c in shortcode)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0)
                return false;

            mediaPk = mediaPk * 64 + index;
        }

        return mediaPk > 0;
    }
}
