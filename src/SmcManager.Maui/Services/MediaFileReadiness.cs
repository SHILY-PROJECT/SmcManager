namespace SmcManager.Maui.Services;

/// <summary>
/// Ожидание готовности локальных медиафайлов сразу после скачивания.
/// </summary>
internal static class MediaFileReadiness
{
    public static async Task WaitForFilesAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsReady(path))
                    break;

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsReady(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0)
                return false;

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
