using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Freezes the tenant's accepted people and work relationships as an immutable roster snapshot.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.freeze", 1)]
public sealed record FreezeWorkforceRosterSnapshot(Uuid TenantId)
    : IRequest<SnapshotRegistration>, IWorkforceRequest, ICallable;
