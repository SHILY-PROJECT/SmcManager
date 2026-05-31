using Android.Content;
using AndroidX.Core.Content;

namespace SmcManager.Maui.Platforms.Android;

/// <summary>
/// Share intent для Android: только медиа через FileProvider + текст в EXTRA_TEXT.
/// </summary>
internal static class AndroidMediaShareHelper
{
    public static Task ShareAsync(string? title, string? text, IReadOnlyList<string> mediaPaths)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
            throw new InvalidOperationException("Activity недоступна.");

        var authority = $"{AppInfo.PackageName}.fileprovider";
        var uris = CreateShareUris(activity, authority, mediaPaths);

        if (uris.Count == 0 && string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        if (uris.Count > 1 && !string.IsNullOrWhiteSpace(text))
            _ = CopyTextToClipboardAsync(text);

        if (uris.Count == 1 && !string.IsNullOrWhiteSpace(text))
        {
            var shareIntent = AndroidX.Core.App.ShareCompat.IntentBuilder.From(activity)
                .SetType(GetMimeType(mediaPaths[0]))
                .SetStream(uris[0])
                .SetText(text)
                .SetSubject(title ?? string.Empty)
                .SetChooserTitle(title ?? "Поделиться")
                .CreateChooserIntent();

            ApplyUriPermissions(shareIntent, uris);
            activity.StartActivity(shareIntent);
            return Task.CompletedTask;
        }

        Intent intent;
        if (uris.Count <= 1)
        {
            intent = new Intent(Intent.ActionSend);
            if (uris.Count == 1)
            {
                intent.SetType(GetMimeType(mediaPaths[0]));
                intent.PutExtra(Intent.ExtraStream, uris[0]);
            }
            else
            {
                intent.SetType("text/plain");
            }
        }
        else
        {
            intent = new Intent(Intent.ActionSendMultiple);
            intent.SetType(ResolveMimeType(mediaPaths));
            intent.PutParcelableArrayListExtra(
                Intent.ExtraStream,
                uris.Cast<global::Android.OS.IParcelable>().ToList());
        }

        if (!string.IsNullOrWhiteSpace(text))
            intent.PutExtra(Intent.ExtraText, text);

        if (!string.IsNullOrWhiteSpace(title))
            intent.PutExtra(Intent.ExtraSubject, title);

        ApplyUriPermissions(intent, uris);

        var chooser = Intent.CreateChooser(intent, title ?? "Поделиться")!;
        ApplyUriPermissions(chooser, uris);
        activity.StartActivity(chooser);
        return Task.CompletedTask;
    }

    private static List<global::Android.Net.Uri> CreateShareUris(
        global::Android.App.Activity activity,
        string authority,
        IReadOnlyList<string> paths)
    {
        var uris = new List<global::Android.Net.Uri>();
        foreach (var path in paths)
        {
            if (!IsShareableAppPath(path))
                continue;

            var javaFile = new Java.IO.File(path);
            if (!javaFile.Exists())
                continue;

            try
            {
                var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, authority, javaFile);
                if (uri is not null)
                    uris.Add(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Android share uri failed for {path}: {ex.Message}");
            }
        }

        return uris;
    }

    private static bool IsShareableAppPath(string path)
    {
        var full = Path.GetFullPath(path);
        var roots = new[]
        {
            Path.GetFullPath(FileSystem.AppDataDirectory),
            Path.GetFullPath(FileSystem.CacheDirectory),
        };

        return roots.Any(root => full.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyUriPermissions(Intent intent, IList<global::Android.Net.Uri> uris)
    {
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        if (uris.Count == 0)
            return;

        var clip = ClipData.NewUri(Platform.AppContext.ContentResolver, "shared", uris[0]);
        for (var i = 1; i < uris.Count; i++)
            clip.AddItem(new ClipData.Item(uris[i]));

        intent.ClipData = clip;
    }

    private static string ResolveMimeType(IReadOnlyList<string> paths)
    {
        var mimeTypes = paths
            .Select(GetMimeType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mimeTypes.Count == 1)
            return mimeTypes[0];

        if (mimeTypes.All(static mime => mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            return "image/*";

        if (mimeTypes.All(static mime => mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
            return "video/*";

        return "*/*";
    }

    private static string GetMimeType(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
            return "*/*";

        var mime = global::Android.Webkit.MimeTypeMap.Singleton?.GetMimeTypeFromExtension(
            extension.TrimStart('.').ToLowerInvariant());
        return string.IsNullOrWhiteSpace(mime) ? "*/*" : mime;
    }

    private static async Task CopyTextToClipboardAsync(string text)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Clipboard.Default.SetTextAsync(text);
            var activity = Platform.CurrentActivity;
            if (activity is null)
                return;

            global::Android.Widget.Toast.MakeText(
                activity,
                "Описание скопировано — вставьте его в сообщение",
                global::Android.Widget.ToastLength.Long)?.Show();
        }).ConfigureAwait(false);
    }
}
