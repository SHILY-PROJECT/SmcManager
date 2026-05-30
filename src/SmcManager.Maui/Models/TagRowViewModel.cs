using CommunityToolkit.Mvvm.ComponentModel;
using SmcManager.Core.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.Models;

/// <summary>
/// Строка тега на странице управления.
/// </summary>
public partial class TagRowViewModel : ObservableObject
{
    public TagRowViewModel(ContentTag tag, int usageCount)
    {
        Tag = tag;
        UsageCount = usageCount;
        EditName = tag.Name;
        EditColorHex = tag.ColorHex;
        UsageText = usageCount switch
        {
            0 => "не используется",
            1 => "1 запись",
            _ => $"{usageCount} записей"
        };
    }

    public ContentTag Tag { get; private set; }

    public string Name => Tag.Name;

    public string ColorHex => Tag.ColorHex;

    public int UsageCount { get; }

    public string UsageText { get; }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editColorHex = TagColorHelper.DefaultHex;

    public void BeginEdit()
    {
        EditName = Tag.Name;
        EditColorHex = Tag.ColorHex;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        EditName = Tag.Name;
        EditColorHex = Tag.ColorHex;
        IsEditing = false;
    }

    public void CommitSave(ContentTag saved)
    {
        Tag = saved;
        EditName = saved.Name;
        EditColorHex = saved.ColorHex;
        IsEditing = false;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ColorHex));
    }
}
