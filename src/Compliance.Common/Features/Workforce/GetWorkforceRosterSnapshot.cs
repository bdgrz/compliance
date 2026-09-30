using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Reads a frozen roster; the manager chain is returned only with its field permission.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.get", 1)]
public sealed record GetWorkforceRosterSnapshot(Uuid TenantId, Uuid SnapshotId)
    : IRequest<WorkforceRosterSnapshotView>, IWorkforceRequest, ICallable;
