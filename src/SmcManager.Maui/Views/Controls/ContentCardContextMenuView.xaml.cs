using CommunityToolkit.Maui.Extensions;

namespace SmcManager.Maui.Views.Controls;

public partial class ContentCardContextMenuView : ContentView
{
    private Page? _hostPage;

    public ContentCardContextMenuView() => InitializeComponent();

    public void AttachToPage(Page page) => _hostPage = page;

    private async void OnShareTapped(object? sender, EventArgs e)
    {
        if (_hostPage is null)
            return;

        await _hostPage.ClosePopupAsync(ContentCardContextAction.Share).ConfigureAwait(false);
    }

    private async void OnDeleteTapped(object? sender, EventArgs e)
    {
        if (_hostPage is null)
            return;

        await _hostPage.ClosePopupAsync(ContentCardContextAction.Delete).ConfigureAwait(false);
    }
}
