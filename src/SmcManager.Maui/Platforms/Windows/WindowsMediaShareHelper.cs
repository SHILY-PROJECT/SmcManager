using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;
using WinRT.Interop;

namespace SmcManager.Maui.Platforms.Windows;

/// <summary>
/// WinUI 3: GetForCurrentView() не работает — нужен HWND окна через COM interop (как в MAUI Essentials).
/// </summary>
internal static class WindowsMediaShareHelper
{
    public static async Task ShareAsync(string? title, string? text, IReadOnlyList<string> paths)
    {
        var mauiWindow = Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("Окно приложения недоступно.");

        if (mauiWindow.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            throw new InvalidOperationException("Окно приложения недоступно.");

        var storageFiles = new List<IStorageItem>();
        foreach (var path in paths)
            storageFiles.Add(await StorageFile.GetFileFromPathAsync(path));

        var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
        var dataTransferManager = DataTransferManagerHelper.GetDataTransferManager(windowHandle);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            dataTransferManager.DataRequested -= OnDataRequested;

            var request = args.Request;
            request.Data.Properties.Title = title ?? "Поделиться";

            if (!string.IsNullOrWhiteSpace(text))
            {
                request.Data.SetText(text);
                request.Data.Properties.Description = text;
            }

            if (storageFiles.Count > 0)
                request.Data.SetStorageItems(storageFiles);

            tcs.TrySetResult();
        }

        dataTransferManager.DataRequested += OnDataRequested;

        try
        {
            if (MainThread.IsMainThread)
            {
                DataTransferManagerHelper.ShowShare(windowHandle);
            }
            else
            {
                await mauiWindow.Dispatcher.DispatchAsync(() =>
                    DataTransferManagerHelper.ShowShare(windowHandle));
            }

            await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            dataTransferManager.DataRequested -= OnDataRequested;
            throw;
        }
    }

    private static class DataTransferManagerHelper
    {
        private static readonly Guid DataTransferManagerId = new(
            0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

        [ComImport]
        [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDataTransferManagerInterop
        {
            IntPtr GetForWindow(IntPtr appWindow, ref Guid riid);
            void ShowShareUIForWindow(IntPtr appWindow);
        }

        public static DataTransferManager GetDataTransferManager(IntPtr appWindow)
        {
            var interop = DataTransferManager.As<IDataTransferManagerInterop>();
            var riid = DataTransferManagerId;
            var handle = interop.GetForWindow(appWindow, ref riid);
            return MarshalInterface<DataTransferManager>.FromAbi(handle);
        }

        public static void ShowShare(IntPtr appWindow)
        {
            var interop = DataTransferManager.As<IDataTransferManagerInterop>();
            interop.ShowShareUIForWindow(appWindow);
        }
    }
}
