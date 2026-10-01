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
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId, snapshotId,
            WorkforceRosterSnapshotContent.Kind, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Failure(new RequestError(
                content.Error.Kind, content.Error.Kind == RequestErrorKind.NotFound
                    ? "The roster snapshot was not found."
                    : "The stored roster snapshot failed integrity verification."));
        var snapshot = content.Value.Snapshot;
        return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Success((
            WorkforceRosterSnapshotContent.ToView(tenantId, snapshot, content.Value.Rows,
                new FieldRedactor(FieldClasses.WorkforceManagerChain, false)),
            snapshot.FrozenAt));
    }
}
