using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class FreezeWorkforceRosterSnapshotHandler(WorkforceRosterSnapshotter snapshotter)
    : IRequestHandler<FreezeWorkforceRosterSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<FreezeWorkforceRosterSnapshot> context, CancellationToken ct) =>
        snapshotter.FreezeAsync(context, null, null, ct);
}
