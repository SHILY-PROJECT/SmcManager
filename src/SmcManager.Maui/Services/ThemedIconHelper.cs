namespace SmcManager.Maui.Services;

/// <summary>
/// Принудительная подстановка PNG-иконок (DynamicResource для строк их не обновляет).
/// </summary>
public static class ThemedIconHelper
{
    public static ImageSource FromFile(string fileName) =>
        ImageSource.FromFile(fileName);

    public static void SetSource(Image? image, string fileName)
    {
        if (image is null || HasFileSource(image.Source, fileName))
            return;

        image.Source = FromFile(fileName);
    }

    public static void SetSource(ImageButton? button, string fileName)
    {
        if (button is null || HasFileSource(button.Source, fileName))
            return;

        button.Source = FromFile(fileName);
    }

    public static void SetImageSource(Button? button, string fileName)
    {
        if (button is null || HasFileSource(button.ImageSource, fileName))
            return;

        button.ImageSource = FromFile(fileName);
    }

    private static bool HasFileSource(ImageSource? source, string fileName) =>
        source is FileImageSource { File: var current }
        && string.Equals(current, fileName, StringComparison.OrdinalIgnoreCase);

    public static void ApplyCarouselIcons(
        ImageButton? prevButton,
        ImageButton? nextButton,
        Button? mediaExpandButton,
        bool isMediaExpanded,
        ThemePalette palette)
    {
        SetSource(prevButton, palette.CarouselPrevIcon);
        SetSource(nextButton, palette.CarouselNextIcon);
        SetImageSource(
            mediaExpandButton,
            isMediaExpanded ? palette.MediaCollapseIcon : palette.MediaExpandIcon);
    }
}
