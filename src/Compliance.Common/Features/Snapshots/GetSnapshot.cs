using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

[Discriminator("bdgrz.snapshot.get", 1)]
public sealed record GetSnapshot(Uuid TenantId, Uuid SnapshotId, long? MinimumRevision = null)
    : IRequest<SnapshotView>, ITenantAccessRequest, ICallable;
