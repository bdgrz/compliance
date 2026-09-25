using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public interface ITenantInvitationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
