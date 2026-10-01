using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Reads an accepted, integrity-verified frozen workforce roster. Campaigns use only identity,
///     worker type, status, start date, and roster team, so the restricted manager chain stays
///     redacted.
/// </summary>
public static class RosterSnapshotSource
{
    public static async ValueTask<Result<(WorkforceRosterSnapshotView Roster, DateTimeOffset FrozenAt)>>
        ReadAsync(IAggregateReader reader, Uuid tenantId, Uuid snapshotId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var snapshot = await reader.HydrateAsync(new PopulationSnapshot(tenantId, snapshotId), ct)
            .ConfigureAwait(false);
        if (!snapshot.IsFrozen || snapshot.Kind != WorkforceRosterSnapshotContent.Kind)
            return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The roster snapshot was not found."));
        if (!snapshot.HasIntactContent)
            return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The stored roster snapshot failed integrity verification."));
        return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Success((
            WorkforceRosterSnapshotContent.ToView(tenantId, snapshot,
                new FieldRedactor(FieldClasses.WorkforceManagerChain, false)),
            snapshot.FrozenAt));
    }
}
