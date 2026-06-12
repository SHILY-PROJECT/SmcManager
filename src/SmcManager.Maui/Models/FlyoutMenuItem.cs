using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmcManager.Maui.Models;

/// <summary>
/// Пункт пользовательского бокового меню Shell.
/// </summary>
public sealed class FlyoutMenuItem : INotifyPropertyChanged
{
    private ImageSource? _icon;

    public FlyoutMenuItem(string title, string route)
    {
        Title = title;
        Route = route;
    }

    public string Title { get; }

    public string Route { get; }

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (_icon == value)
                return;

            _icon = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
