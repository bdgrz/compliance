using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceSourceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
