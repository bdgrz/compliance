using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceObservationProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
