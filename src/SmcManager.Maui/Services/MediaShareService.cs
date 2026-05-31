using SmcManager.Core.Interfaces;

namespace SmcManager.Maui.Services;

/// <summary>
/// «Поделиться» с текстом и медиа: на Android/Windows текст идёт как caption, не как .txt файл.
/// </summary>
public sealed class MediaShareService : IMediaShareService
{
    public Task ShareAsync(string? title, string? text, IReadOnlyList<string> filePaths)
    {
        var paths = filePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0 && string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

#if ANDROID
        return ShareAndroidAsync(title, text, paths);
#elif WINDOWS
        return Platforms.Windows.WindowsMediaShareHelper.ShareAsync(title, text, paths);
#else
        return ShareFallbackAsync(title, text, paths);
#endif
    }

#if ANDROID
    private static Task ShareAndroidAsync(string? title, string? text, IReadOnlyList<string> paths)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
            throw new InvalidOperationException("Activity недоступна.");

        var authority = $"{AppInfo.PackageName}.fileprovider";
        var uris = new List<Android.Net.Uri>();
        foreach (var path in paths)
        {
            var javaFile = new Java.IO.File(path);
            if (!javaFile.Exists())
                continue;

            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, authority, javaFile);
            if (uri is not null)
                uris.Add(uri);
        }

        if (uris.Count == 0 && string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        Android.Content.Intent intent;
        if (uris.Count <= 1)
        {
            intent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
            if (uris.Count == 1)
            {
                intent.SetType(GetAndroidMimeType(paths[0]));
                intent.PutExtra(Android.Content.Intent.ExtraStream, uris[0]);
            }
            else
            {
                intent.SetType("text/plain");
            }
        }
        else
        {
            intent = new Android.Content.Intent(Android.Content.Intent.ActionSendMultiple);
            intent.SetType(ResolveAndroidMimeType(paths));
            IList<Android.OS.IParcelable> parcelables = uris.Cast<Android.OS.IParcelable>().ToList();
            intent.PutParcelableArrayListExtra(Android.Content.Intent.ExtraStream, parcelables);
        }

        if (!string.IsNullOrWhiteSpace(text))
            intent.PutExtra(Android.Content.Intent.ExtraText, text);

        if (!string.IsNullOrWhiteSpace(title))
            intent.PutExtra(Android.Content.Intent.ExtraSubject, title);

        intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);

        var chooser = Android.Content.Intent.CreateChooser(intent, title ?? "Поделиться");
        chooser!.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
        activity.StartActivity(chooser);
        return Task.CompletedTask;
    }

    private static string ResolveAndroidMimeType(IReadOnlyList<string> paths)
    {
        var mimeTypes = paths
            .Select(GetAndroidMimeType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return mimeTypes.Count == 1 ? mimeTypes[0] : "*/*";
    }

    private static string GetAndroidMimeType(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
            return "*/*";

        var mime = Android.Webkit.MimeTypeMap.Singleton?.GetMimeTypeFromExtension(
            extension.TrimStart('.').ToLowerInvariant());
        return string.IsNullOrWhiteSpace(mime) ? "*/*" : mime;
    }
#endif

    private static async Task ShareFallbackAsync(string? title, string? text, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Text = text!
            });
            return;
        }

        if (paths.Count == 1 && string.IsNullOrWhiteSpace(text))
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(paths[0])
            });
            return;
        }

        var files = paths.Select(static path => new ShareFile(path)).ToList();
        await Share.Default.RequestAsync(new ShareMultipleFilesRequest
        {
            Title = title,
            Files = files
        });
    }
}
