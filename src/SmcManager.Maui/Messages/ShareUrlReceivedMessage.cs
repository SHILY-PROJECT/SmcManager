namespace SmcManager.Maui.Messages;

/// <summary>
/// Сообщение MVVM о новой ссылке из Share / буфера.
/// </summary>
public class ShareUrlReceivedMessage
{
    public ShareUrlReceivedMessage(string url) => Url = url;

    public string Url { get; }
}
