using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public interface IResponsibilitySetDirectory
{
    ValueTask<ResponsibilitySetView?> GetAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default);
}

public interface IResponsibilitySetProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
