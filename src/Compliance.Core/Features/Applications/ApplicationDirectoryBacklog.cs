using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// Inspects tenant events after the ApplicationDirectoryV2 checkpoint. The scan stops at
/// <see cref="ScanLimit"/> so a lagging worker costs one bounded read, never tenant history.
/// </summary>
public static class ApplicationDirectoryBacklog
{
    public const int ScanLimit = 256;

    public static async ValueTask<ApplicationDirectoryBacklogScan> FindAsync(
        IApplicationDirectoryReader directory, IDomainEventReader events, Uuid tenantId,
        Func<DomainEvent, bool> match, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        var scanned = 0;
        await foreach (var pending in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString()), checkpoint.Cursor, ct).ConfigureAwait(false))
        {
            // Exceeded means a record exists beyond the limit, not that the limit was reached.
            if (scanned++ == ScanLimit)
                return new ApplicationDirectoryBacklogScan(null, true);
            if (match(pending.Event))
                return new ApplicationDirectoryBacklogScan(pending.Event, false);
        }
        return default;
    }
}
