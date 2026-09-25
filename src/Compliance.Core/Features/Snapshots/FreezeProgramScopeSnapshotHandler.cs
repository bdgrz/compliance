using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class FreezeProgramScopeSnapshotHandler(ScopeSnapshotFreezer freezer)
    : IRequestHandler<FreezeProgramScopeSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<FreezeProgramScopeSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        return freezer.FreezeAsync(context, request.TenantId, request.ProgramId,
            request.ExpectedProgramRevision, request.BoundaryId,
            request.ApprovedBoundaryVersionId, null, null, ct);
    }
}
