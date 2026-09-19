using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public interface ITenantInvitationDelivery
{
    ValueTask SendAsync(Uuid tenantId, string emailAddress, string token, CancellationToken ct);
}
