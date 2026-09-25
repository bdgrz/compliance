using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationBoundaryReferenceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
