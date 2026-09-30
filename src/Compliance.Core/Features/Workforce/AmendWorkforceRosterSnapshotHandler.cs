using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class AmendWorkforceRosterSnapshotHandler(WorkforceRosterSnapshotter snapshotter)
    : IRequestHandler<AmendWorkforceRosterSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<AmendWorkforceRosterSnapshot> context, CancellationToken ct) =>
        snapshotter.FreezeAsync(context, context.Request.SnapshotId,
            context.Request.Reason ?? string.Empty, ct);
}
