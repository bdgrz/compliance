using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkRelationshipDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
