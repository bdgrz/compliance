using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class GetWorkforceRosterSnapshotHandler(IAggregateReader reader,
    IPermissionAuthorizer permissions)
    : IRequestHandler<GetWorkforceRosterSnapshot, WorkforceRosterSnapshotView>
{
    public ValueTask<Result<WorkforceRosterSnapshotView>> HandleAsync(
        IRequestContext<GetWorkforceRosterSnapshot> context, CancellationToken ct) =>
        WorkforceRosterSnapshotReads.ReadAsync(context, reader, permissions,
            context.Request.TenantId, context.Request.SnapshotId, ct);
}
