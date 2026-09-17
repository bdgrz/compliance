using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Materializes membership events into the queryable tenant-membership directory.</summary>
public interface ITenantMembershipDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
