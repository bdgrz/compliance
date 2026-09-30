using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Lists roster snapshot identities and lineage, newest first, without roster rows.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.list", 1)]
public sealed record ListWorkforceRosterSnapshots(Uuid TenantId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<PopulationSnapshotSummary>>, IWorkforceRequest, ICallable;
