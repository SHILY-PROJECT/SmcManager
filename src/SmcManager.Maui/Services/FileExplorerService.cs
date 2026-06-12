using SmcManager.Core.Interfaces;

namespace SmcManager.Maui.Services;

/// <summary>
/// Кроссплатформенное открытие файлов и папок в проводнике / файловом менеджере.
/// </summary>
public class FileExplorerService : IFileExplorerService
{
    public Task OpenFileInExplorerAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return Task.CompletedTask;

#if WINDOWS
        var argument = $"/select,\"{filePath}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = argument,
            UseShellExecute = true
        });
        return Task.CompletedTask;
#elif ANDROID
        return OpenAndroidPathAsync(filePath, isFolder: false);
#else
        return Launcher.Default.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(filePath)
        });
#endif
    }

    public Task OpenFolderInExplorerAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return Task.CompletedTask;

#if WINDOWS
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
        return Task.CompletedTask;
#elif ANDROID
        return OpenAndroidPathAsync(folderPath, isFolder: true);
#else
        return Task.CompletedTask;
#endif
    }

#if ANDROID
    private static Task OpenAndroidPathAsync(string path, bool isFolder)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return Task.CompletedTask;

        var javaFile = new Java.IO.File(path);
        if (!javaFile.Exists()) return Task.CompletedTask;

        var target = isFolder ? javaFile : javaFile.ParentFile ?? javaFile;
        var authority = $"{AppInfo.PackageName}.fileprovider";
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, authority, target);

        var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
        intent.SetDataAndType(uri, isFolder ? "vnd.android.document/directory" : "*/*");
        intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);

        try
        {
            activity.StartActivity(Android.Content.Intent.CreateChooser(intent, "Открыть"));
        }
        catch
        {
            if (!isFolder)
            {
                var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, authority, javaFile);
                var fileIntent = new Android.Content.Intent(Android.Content.Intent.ActionView);
                fileIntent.SetDataAndType(fileUri, "*/*");
                fileIntent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
                fileIntent.AddFlags(Android.Content.ActivityFlags.NewTask);
                activity.StartActivity(Android.Content.Intent.CreateChooser(fileIntent, "Открыть файл"));
            }
        }

        return Task.CompletedTask;
    }
#endif
}
