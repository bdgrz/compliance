using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationRelationshipDirectory
{
    ValueTask<Page<ApplicationRelationshipView>> ListAsync(Uuid tenantId,
        Uuid applicationId, string direction, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}

public interface IApplicationRelationshipProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
