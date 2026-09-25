using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class AmendProgramScopeSnapshotHandler(ScopeSnapshotFreezer freezer)
    : IRequestHandler<AmendProgramScopeSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<AmendProgramScopeSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        return freezer.FreezeAsync(context, request.TenantId, request.ProgramId,
            request.ExpectedProgramRevision, request.BoundaryId,
            request.ApprovedBoundaryVersionId, request.SnapshotId, request.Reason, ct);
    }
}
