using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record WorkQueueReadFence(ProjectionCheckpoint TenantCheckpoint,
    ProjectionCheckpoint? EvidenceWorkItemCheckpoint);
