using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Reads a roster snapshot from its immutable source and applies manager-chain redaction.</summary>
static class WorkforceRosterSnapshotReads
{
    public static async ValueTask<Result<WorkforceRosterSnapshotView>> ReadAsync<TRequest>(
        IRequestContext<TRequest> context, IAggregateReader reader,
        IPermissionAuthorizer permissions, Uuid tenantId, Uuid snapshotId, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var snapshot = await reader.HydrateAsync(new PopulationSnapshot(tenantId, snapshotId), ct)
            .ConfigureAwait(false);
        if (!snapshot.IsFrozen || snapshot.Kind != WorkforceRosterSnapshotContent.Kind)
            return Result<WorkforceRosterSnapshotView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The roster snapshot was not found."));
        if (!snapshot.HasIntactContent)
            return Result<WorkforceRosterSnapshotView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The stored roster snapshot failed integrity verification."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        return Result<WorkforceRosterSnapshotView>.Success(
            WorkforceRosterSnapshotContent.ToView(tenantId, snapshot, managerChain));
    }
}
