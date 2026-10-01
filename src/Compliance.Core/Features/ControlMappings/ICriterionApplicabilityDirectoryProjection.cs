using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public interface ICriterionApplicabilityDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
