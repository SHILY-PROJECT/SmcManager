using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Download;

internal static class DownloadTagHelper
{
    public static async Task ApplyTagsAsync(
        IContentRepository repository,
        ContentItem content,
        DownloadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TagIds.Count == 0)
            return;

        await repository.AssignTagsAsync(content.Id, request.TagIds, cancellationToken);
        content.Tags = (await repository.GetContentByIdAsync(content.Id, cancellationToken))?.Tags ?? [];
    }
}
