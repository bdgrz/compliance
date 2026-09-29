using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IAccessGrantDirectory
{
    ValueTask<AccessGrantSetView> ListAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<AccessGrantView?> GetAsync(Uuid tenantId, Uuid grantId, CancellationToken ct = default);

    ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId, IReadOnlySet<Uuid> grantIds,
        CancellationToken ct = default);
}

public sealed record ProgramAccessVisibility(bool OrganizationWide, IReadOnlySet<Uuid> ProgramIds)
{
    public bool HasAnyAccess => OrganizationWide || ProgramIds.Count > 0;
}

public interface IAccessGrantProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
