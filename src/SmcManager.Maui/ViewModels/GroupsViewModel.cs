using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Раздел «Группы» — фильтрация контента по тегам.
/// </summary>
public partial class GroupsViewModel : ObservableObject,
    IRecipient<TagsChangedMessage>,
    IRecipient<ContentDeletedMessage>
{
    private readonly IContentRepository _repository;
    private readonly IAppStoragePaths _storagePaths;

    public GroupsViewModel(IContentRepository repository, IAppStoragePaths storagePaths)
    {
        _repository = repository;
        _storagePaths = storagePaths;
        WeakReferenceMessenger.Default.Register<TagsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<ContentDeletedMessage>(this);
    }

    public ObservableCollection<TagChipViewModel> TagChips { get; } = [];

    public ObservableCollection<ContentItemDisplayModel> FilteredItems { get; } = [];

    [ObservableProperty]
    private ContentTag? _selectedTag;

    [ObservableProperty]
    private string _emptyTagsHint = string.Empty;

    partial void OnSelectedTagChanged(ContentTag? value)
    {
        UpdateChipSelection();
        _ = LoadFilteredAsync();
    }

    [RelayCommand]
    private async Task AppearingAsync() => await LoadTagsAsync();

    [RelayCommand]
    private void SelectTag(TagChipViewModel chip) => SelectedTag = chip.Tag;

    public void Receive(TagsChangedMessage message) => _ = LoadTagsAsync();

    public void Receive(ContentDeletedMessage message) => _ = LoadFilteredAsync();

    [RelayCommand]
    private async Task DeleteItemAsync(ContentItemDisplayModel item)
    {
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, item))
            return;

        FilteredItems.Remove(item);
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _repository.GetTagsAsync();
        TagChips.Clear();

        foreach (var tag in tags)
            TagChips.Add(new TagChipViewModel(tag));

        EmptyTagsHint = TagChips.Count == 0
            ? "Создайте теги в разделе «Теги» в боковом меню."
            : string.Empty;

        SelectedTag = TagChips.FirstOrDefault()?.Tag;
        UpdateChipSelection();
    }

    private void UpdateChipSelection()
    {
        foreach (var chip in TagChips)
            chip.IsSelected = SelectedTag is not null && chip.Tag.Id == SelectedTag.Id;
    }

    private async Task LoadFilteredAsync()
    {
        FilteredItems.Clear();
        if (SelectedTag is null) return;

        var items = await _repository.GetContentByTagAsync(SelectedTag.Id);
        foreach (var item in items)
            FilteredItems.Add(ContentItemDisplayModel.FromEntity(item, _storagePaths.DownloadsPath));
    }
}
