using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Управление пользовательскими тегами.
/// </summary>
public partial class TagsViewModel : ObservableObject,
    IRecipient<TagSortChangedMessage>
{
    private readonly IContentRepository _repository;
    private readonly TagCreationService _tagCreation;
    private readonly TagListService _tagList;
    private readonly BottomToastService _toast;
    private readonly TagColorPickerService _colorPicker;

    public TagsViewModel(
        IContentRepository repository,
        TagCreationService tagCreation,
        TagListService tagList,
        BottomToastService toast,
        TagColorPickerService colorPicker)
    {
        _repository = repository;
        _tagCreation = tagCreation;
        _tagList = tagList;
        _toast = toast;
        _colorPicker = colorPicker;
        WeakReferenceMessenger.Default.Register<TagSortChangedMessage>(this);
    }

    public ObservableCollection<TagRowViewModel> Tags { get; } = [];

    public IReadOnlyList<string> EmojiSuggestions { get; } = TagEmojiLibrary.Suggested;

    [ObservableProperty]
    private string _newTagName = string.Empty;

    [ObservableProperty]
    private string _selectedColor = TagColorHelper.DefaultHex;

    [ObservableProperty]
    private string? _statusMessage;

    public void Receive(TagSortChangedMessage message) => _ = LoadAsync();

    [RelayCommand]
    private async Task AppearingAsync() => await LoadAsync();

    [RelayCommand]
    private void AppendEmoji(string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            return;

        var trimmed = NewTagName.Trim();
        if (trimmed.StartsWith(emoji, StringComparison.Ordinal))
            return;

        var combined = string.IsNullOrWhiteSpace(trimmed) ? $"{emoji} " : $"{emoji} {trimmed}";
        NewTagName = combined.Length <= 32 ? combined : NewTagName;
    }

    [RelayCommand]
    private async Task PickNewTagColorAsync()
    {
        var color = await _colorPicker.PickColorAsync(SelectedColor);
        if (color is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
            SelectedColor = TagColorHelper.NormalizeHex(color));
    }

    [RelayCommand]
    private async Task PickRowColorAsync(TagRowViewModel row)
    {
        if (!row.IsEditing)
            return;

        var color = await _colorPicker.PickColorAsync(row.EditColorHex);
        if (color is null)
            return;

        var normalized = TagColorHelper.NormalizeHex(color);
        await MainThread.InvokeOnMainThreadAsync(() => row.EditColorHex = normalized);
    }

    [RelayCommand]
    private async Task AddTagAsync()
    {
        var (created, newTag, createError) = await _tagCreation.TryCreateAsync(NewTagName, SelectedColor);
        if (!created)
        {
            await _toast.ShowWarningAsync(createError ?? "Заполните поле.");
            return;
        }

        NewTagName = string.Empty;
        SelectedColor = TagColorHelper.DefaultHex;
        await LoadAsync();
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        StatusMessage = $"Тег «{newTag!.Name}» добавлен.";
    }

    [RelayCommand]
    private void EditTag(TagRowViewModel row)
    {
        foreach (var tag in Tags)
        {
            if (tag.IsEditing && tag != row)
                tag.CancelEdit();
        }

        row.BeginEdit();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveRowTagAsync(TagRowViewModel row)
    {
        var (success, tag, error) = await _tagCreation.TryUpdateAsync(row.Tag.Id, row.EditName, row.EditColorHex);
        if (!success)
        {
            await _toast.ShowWarningAsync(error ?? "Заполните поле.");
            return;
        }

        row.CommitSave(tag!);
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        StatusMessage = $"Тег «{tag!.Name}» сохранён.";
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelRowEdit(TagRowViewModel row)
    {
        row.CancelEdit();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task DeleteTagAsync(TagRowViewModel row)
    {
        if (row.IsEditing)
            row.CancelEdit();

        var count = await _repository.CountContentByTagAsync(row.Tag.Id);
        var message = count > 0
            ? $"Удалить тег «{row.Tag.Name}»? С {count} записей тег будет снят."
            : $"Удалить тег «{row.Tag.Name}»?";

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;

        var confirm = await page.DisplayAlertAsync("Удаление тега", message, "Удалить", "Отмена");
        if (!confirm) return;

        await _repository.DeleteTagAsync(row.Tag.Id);
        await LoadAsync();
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        StatusMessage = "Тег удалён.";
    }

    private async Task LoadAsync()
    {
        var editingState = Tags
            .Where(t => t.IsEditing)
            .ToDictionary(t => t.Tag.Id, t => (t.EditName, t.EditColorHex));

        var tags = await _tagList.GetSortedTagsAsync();
        var usageCounts = await _repository.GetTagUsageCountsAsync();
        Tags.Clear();
        foreach (var tag in tags)
        {
            usageCounts.TryGetValue(tag.Id, out var usage);
            var row = new TagRowViewModel(tag, usage);
            if (editingState.TryGetValue(tag.Id, out var edit))
            {
                row.EditName = edit.EditName;
                row.EditColorHex = edit.EditColorHex;
                row.IsEditing = true;
            }

            Tags.Add(row);
        }
    }
}
