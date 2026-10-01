using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceObservationResolutionProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
