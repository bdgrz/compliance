using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Captures the accepted roster only after both roster projections have reached their sources
///     and proves neither advanced during the read (a fenced capture), then freezes it through the reusable population snapshot primitive (R1-11d on EN-03).
/// </summary>
public sealed class WorkforceRosterSnapshotter(IPersonDirectoryReader people,
    PersonReadConsistency peopleConsistency, IWorkRelationshipDirectoryReader relationships,
    WorkRelationshipReadConsistency relationshipConsistency, PopulationSnapshotFreezer freezer)
{
    public async ValueTask<Result<SnapshotRegistration>> FreezeAsync<TRequest>(
        IRequestContext<TRequest> context, Uuid? amendsSnapshotId, string? reason,
        CancellationToken ct) where TRequest : IRequestBase
    {
        var tenantId = ((IWorkforceRequest)context.Request).TenantId;
        var peopleFence = await peopleConsistency.CaptureListFenceAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!peopleFence.IsSuccess)
            return Result<SnapshotRegistration>.Failure(peopleFence.Error);
        var relationshipsFence = await relationshipConsistency.CaptureListFenceAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!relationshipsFence.IsSuccess)
            return Result<SnapshotRegistration>.Failure(relationshipsFence.Error);

        var roster = await ReadAllAsync((cursor, token) =>
            people.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        var jobs = await ReadAllAsync((cursor, token) =>
            relationships.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        if (roster is null || jobs is null)
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                $"A roster snapshot currently supports at most {PopulationSnapshot.MaximumInlineRows} rows."));
        // Fence: pages are read in separate transactions, so a projection that advanced after the
        // catch-up check could mix versions. Refuse rather than freeze a torn roster.
        var peopleHeld = await peopleConsistency.EnsureFenceHeldAsync(tenantId, peopleFence.Value,
            ct).ConfigureAwait(false);
        if (!peopleHeld.IsSuccess)
            return Result<SnapshotRegistration>.Failure(peopleHeld.Error);
        var relationshipsHeld = await relationshipConsistency.EnsureFenceHeldAsync(tenantId,
            relationshipsFence.Value, ct).ConfigureAwait(false);
        if (!relationshipsHeld.IsSuccess)
            return Result<SnapshotRegistration>.Failure(relationshipsHeld.Error);
        if (roster.Any(person => person.TenantId != tenantId) ||
            jobs.Any(job => job.TenantId != tenantId))
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The roster projection returned content outside the tenant."));

        return await freezer.FreezeAsync(context, tenantId, WorkforceRosterSnapshotContent.Kind,
            WorkforceRosterSnapshotContent.Rows(roster, jobs), amendsSnapshotId, reason,
            WorkforceActor.From(context), ct).ConfigureAwait(false);
    }

    static async ValueTask<List<T>?> ReadAllAsync<T>(
        Func<string?, CancellationToken, ValueTask<Page<T>>> read, CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await read(cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            if (items.Count > PopulationSnapshot.MaximumInlineRows)
                return null;
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }
}
