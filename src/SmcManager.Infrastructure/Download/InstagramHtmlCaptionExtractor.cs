using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Извлечение текста описания Instagram из HTML страницы поста / embed.
/// </summary>
internal static partial class InstagramHtmlCaptionExtractor
{
    [GeneratedRegex(
        @"""caption""\s*:\s*\{[^{}]*""text""\s*:\s*""((?:\\.|[^""\\])*)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CaptionObjectRegex();

    [GeneratedRegex(
        @"""edge_media_to_caption""\s*:\s*\{[^{}]*""edges""\s*:\s*\[\s*\{[^{}]*""text""\s*:\s*""((?:\\.|[^""\\])*)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EdgeCaptionRegex();

    [GeneratedRegex(
        @"""accessibility_caption""\s*:\s*""((?:\\.|[^""\\])*)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccessibilityCaptionRegex();

    [GeneratedRegex(
        @"property=""og:description""\s+content=""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgDescriptionRegex();

    public static string? FromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        foreach (var pattern in new Func<string, string?>[]
                 {
                     TryCaptionObject,
                     TryEdgeCaption,
                     TryAccessibilityCaption,
                     TryOgDescription
                 })
        {
            var caption = pattern(html);
            if (!string.IsNullOrWhiteSpace(caption))
                return caption;
        }

        return null;
    }

    private static string? TryCaptionObject(string html) =>
        Normalize(PickFirstMatch(CaptionObjectRegex(), html));

    private static string? TryEdgeCaption(string html) =>
        Normalize(PickFirstMatch(EdgeCaptionRegex(), html));

    private static string? TryAccessibilityCaption(string html) =>
        Normalize(PickFirstMatch(AccessibilityCaptionRegex(), html));

    private static string? TryOgDescription(string html) =>
        NormalizeOgDescription(PickFirstMatch(OgDescriptionRegex(), html));

    private static string? PickFirstMatch(Regex regex, string html)
    {
        var match = regex.Match(html);
        if (!match.Success)
            return null;

        return DecodeJsonString(match.Groups[1].Value);
    }

    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        return text.Length == 0 ? null : text;
    }

    private static string? NormalizeOgDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        const string marker = " on Instagram:";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            text = text[(idx + marker.Length)..].Trim();
            text = text.Trim('"', '\'', '«', '»', '“', '”', ':', ' ');
        }

        return text.Length == 0 ? null : text;
    }

    private static string DecodeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        try
        {
            return JsonSerializer.Deserialize<string>($"\"{value}\"") ?? value;
        }
        catch
        {
            return value
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\/", "/", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }
    }
}
