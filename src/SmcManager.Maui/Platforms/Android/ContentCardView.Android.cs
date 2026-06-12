#if ANDROID
namespace SmcManager.Maui.Views.Controls;

public partial class ContentCardView
{
    partial void InitPlatformInteractions() => SetupAndroidLongPress();
}
#endif
