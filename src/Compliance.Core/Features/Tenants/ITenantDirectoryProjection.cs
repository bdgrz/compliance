using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Materializes tenant-registration events into the queryable tenant directory.</summary>
public interface ITenantDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
