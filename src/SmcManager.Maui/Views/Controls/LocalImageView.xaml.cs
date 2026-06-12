using SmcManager.Maui.Services;

namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Локальный путь в Image.Source без ImageSource во ViewModel.
/// </summary>
public partial class LocalImageView : ContentView
{
    public static readonly BindableProperty ImagePathProperty = BindableProperty.Create(
        nameof(ImagePath), typeof(string), typeof(LocalImageView), null,
        propertyChanged: OnImagePathChanged);

    public static readonly BindableProperty ImageAspectProperty = BindableProperty.Create(
        nameof(ImageAspect), typeof(Aspect), typeof(LocalImageView), Aspect.AspectFill);

    public static readonly BindableProperty ImageWidthProperty = BindableProperty.Create(
        nameof(ImageWidth), typeof(double), typeof(LocalImageView), -1.0);

    public static readonly BindableProperty ImageHeightProperty = BindableProperty.Create(
        nameof(ImageHeight), typeof(double), typeof(LocalImageView), -1.0);

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public Aspect ImageAspect
    {
        get => (Aspect)GetValue(ImageAspectProperty);
        set => SetValue(ImageAspectProperty, value);
    }

    public double ImageWidth
    {
        get => (double)GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public double ImageHeight
    {
        get => (double)GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public LocalImageView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateImage();
        HandlerChanged += (_, _) => UpdateImage();
    }

    private static void OnImagePathChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LocalImageView view)
            view.UpdateImage();
    }

    private void UpdateImage()
    {
        if (PreviewImage is null || Handler is null)
            return;

        try
        {
            PreviewImage.Source = RemoteImageCache.SourceFromPathOrUrl(ImagePath);
        }
        catch
        {
            PreviewImage.Source = null;
        }
    }
}
