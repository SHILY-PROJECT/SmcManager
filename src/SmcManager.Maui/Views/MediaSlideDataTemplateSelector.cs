using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Отдельные шаблоны для фото и видео, чтобы MediaElement не создавался на слайдах с изображениями.
/// </summary>
public class MediaSlideDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ImageTemplate { get; set; }

    public DataTemplate? VideoTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        item is MediaSlideViewModel { IsVideo: true }
            ? VideoTemplate ?? throw new InvalidOperationException($"{nameof(VideoTemplate)} is not set.")
            : ImageTemplate ?? throw new InvalidOperationException($"{nameof(ImageTemplate)} is not set.");
}
