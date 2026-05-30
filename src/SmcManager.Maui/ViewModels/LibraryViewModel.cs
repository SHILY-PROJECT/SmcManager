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
public partial class LibraryViewModel : ObservableObject,
    IRecipient<ContentDeletedMessage>,
    IRecipient<TagSortChangedMessage>
{
    private readonly IContentRepository _repository;
    private readonly IAppStoragePaths _storagePaths;
    private readonly TagListService _tagList;

    public LibraryViewModel(
        IContentRepository repository,
        IAppStoragePaths storagePaths,
        TagListService tagList)
    {
        _repository = repository;
        _storagePaths = storagePaths;
        _tagList = tagList;
        WeakReferenceMessenger.Default.Register<ContentDeletedMessage>(this);
        WeakReferenceMessenger.Default.Register<TagSortChangedMessage>(this);
    }

    public ObservableCollection<ContentItemDisplayModel> Items { get; } = [];

    public bool HasNoItems => Items.Count == 0;

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
            {
                var orderedTags = await _tagList.SortTagsAsync(item.Tags);
                Items.Add(ContentItemDisplayModel.FromEntity(
                    item,
                    _storagePaths.DownloadsPath,
                    orderedTags));
            }
        }
        finally
        {
            IsRefreshing = false;
            OnPropertyChanged(nameof(HasNoItems));
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ContentItemDisplayModel item)
    {
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, item))
            return;

        Items.Remove(item);
        OnPropertyChanged(nameof(HasNoItems));
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
    }

    [RelayCommand]
    private Task OpenContentAsync(ContentItemDisplayModel? item) =>
        item is null
            ? Task.CompletedTask
            : ContentNavigationHelper.OpenDetailAsync(item.Id);

    public void Receive(ContentDeletedMessage message) => _ = RefreshAsync();

    public void Receive(TagSortChangedMessage message) => _ = RefreshAsync();
}
