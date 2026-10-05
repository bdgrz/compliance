using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record WorkQueueReadFence(ProjectionCheckpoint TenantCheckpoint,
    IReadOnlyList<WorkItemProjectionFence> WorkItemProjections);

public sealed record WorkItemProjectionFence(string ProjectorName,
    ProjectionCheckpoint Checkpoint);
