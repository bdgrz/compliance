using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public interface ICriteriaTextOverlayDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
