using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Вкладка «Весь контент» — список всех скачанных материалов.
/// </summary>
public partial class LibraryViewModel : ObservableObject, IRecipient<ContentDeletedMessage>
{
    private readonly IContentRepository _repository;
    private readonly IAppStoragePaths _storagePaths;

    public LibraryViewModel(IContentRepository repository, IAppStoragePaths storagePaths)
    {
        _repository = repository;
        _storagePaths = storagePaths;
        WeakReferenceMessenger.Default.Register(this);
    }

    public ObservableCollection<ContentItemDisplayModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [RelayCommand]
    private async Task AppearingAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            var items = await _repository.GetAllContentAsync();
            Items.Clear();
            foreach (var item in items)
                Items.Add(ContentItemDisplayModel.FromEntity(item, _storagePaths.DownloadsPath));
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ContentItemDisplayModel item)
    {
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, item))
            return;

        Items.Remove(item);
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
    }

    public void Receive(ContentDeletedMessage message) => _ = RefreshAsync();
}
