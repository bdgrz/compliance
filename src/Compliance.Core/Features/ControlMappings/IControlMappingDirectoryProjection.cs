using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public interface IControlMappingDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
