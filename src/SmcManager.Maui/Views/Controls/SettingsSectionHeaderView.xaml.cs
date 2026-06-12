namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Заголовок секции настроек: логическая иконка и название.
/// </summary>
public partial class SettingsSectionHeaderView : Grid
{
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(SettingsSectionHeaderView), string.Empty,
            propertyChanged: OnIconChanged);

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(SettingsSectionHeaderView), string.Empty,
            propertyChanged: OnTitleChanged);

    public SettingsSectionHeaderView()
    {
        InitializeComponent();
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static void OnIconChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SettingsSectionHeaderView view)
            view.IconLabel.Text = (string)newValue;
    }

    private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SettingsSectionHeaderView view)
            view.TitleLabel.Text = (string)newValue;
    }
}
