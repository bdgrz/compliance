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
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId, snapshotId,
            WorkforceRosterSnapshotContent.Kind, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return Result<WorkforceRosterSnapshotView>.Failure(new RequestError(content.Error.Kind,
                content.Error.Kind == RequestErrorKind.NotFound
                    ? "The roster snapshot was not found."
                    : "The stored roster snapshot failed integrity verification."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        return Result<WorkforceRosterSnapshotView>.Success(
            WorkforceRosterSnapshotContent.ToView(tenantId, content.Value.Snapshot,
                content.Value.Rows, managerChain));
    }
}
