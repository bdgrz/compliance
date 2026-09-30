using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Reads the latest roster snapshot frozen at or before an instant.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.as_of", 1)]
public sealed record GetWorkforceRosterSnapshotAsOf(Uuid TenantId, DateTimeOffset AsOf)
    : IRequest<WorkforceRosterSnapshotView>, IWorkforceRequest, ICallable;
