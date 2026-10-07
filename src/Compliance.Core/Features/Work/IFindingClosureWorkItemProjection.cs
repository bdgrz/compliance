using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IFindingClosureWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
