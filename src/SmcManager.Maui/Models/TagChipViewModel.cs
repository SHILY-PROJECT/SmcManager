using CommunityToolkit.Mvvm.ComponentModel;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Models;

/// <summary>
/// Тег в горизонтальной ленте «Группы».
/// </summary>
public partial class TagChipViewModel : ObservableObject
{
    public TagChipViewModel(ContentTag tag) => Tag = tag;

    public ContentTag Tag { get; }

    public string Name => Tag.Name;

    public string ColorHex => Tag.ColorHex;

    [ObservableProperty]
    private bool _isSelected;
}
