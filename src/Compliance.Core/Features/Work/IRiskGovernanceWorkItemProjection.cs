using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IRiskGovernanceWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
