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
public partial class TagsViewModel : ObservableObject
{
    private readonly IContentRepository _repository;
    private readonly TagCreationService _tagCreation;
    private readonly BottomToastService _toast;

    public TagsViewModel(
        IContentRepository repository,
        TagCreationService tagCreation,
        BottomToastService toast)
    {
        _repository = repository;
        _tagCreation = tagCreation;
        _toast = toast;
    }

    public ObservableCollection<TagRowViewModel> Tags { get; } = [];

    [ObservableProperty]
    private string _newTagName = string.Empty;

    [ObservableProperty]
    private string _selectedColor = TagColorHelper.DefaultHex;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private async Task AppearingAsync() => await LoadAsync();

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

        row.CancelEdit();
        await LoadAsync();
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        StatusMessage = $"Тег «{tag!.Name}» сохранён.";
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
        var tags = await _repository.GetTagsAsync();
        Tags.Clear();
        foreach (var tag in tags)
        {
            var usage = await _repository.CountContentByTagAsync(tag.Id);
            Tags.Add(new TagRowViewModel(tag, usage));
        }
    }
}
