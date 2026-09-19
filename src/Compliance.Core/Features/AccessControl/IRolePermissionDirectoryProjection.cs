using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Materializes role-permission events into the queryable role-permission directory.</summary>
public interface IRolePermissionDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
