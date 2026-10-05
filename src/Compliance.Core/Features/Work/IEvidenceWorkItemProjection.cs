using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IEvidenceWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
