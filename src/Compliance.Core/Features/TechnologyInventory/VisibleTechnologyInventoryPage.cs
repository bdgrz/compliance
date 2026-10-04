using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

static class VisibleTechnologyInventoryPage
{
    public static async ValueTask<Page<T>> ReadAsync<T>(int limit, string? cursor,
        Func<int, string?, ValueTask<Page<T>>> read,
        Func<T, ValueTask<bool>> isVisible, Func<T, bool> belongsToRequest)
    {
        var visible = new List<T>(limit);
        var scanCursor = cursor;
        while (true)
        {
            var page = await read(limit - visible.Count, scanCursor).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (!belongsToRequest(item))
                    throw new ForeignDirectoryItemException();
                if (await isVisible(item).ConfigureAwait(false))
                    visible.Add(item);
            }

            if (visible.Count == limit || page.NextCursor is null)
                return new Page<T>(visible, page.NextCursor);
            scanCursor = page.NextCursor;
        }
    }

    public sealed class ForeignDirectoryItemException : Exception
    {
    }
}
