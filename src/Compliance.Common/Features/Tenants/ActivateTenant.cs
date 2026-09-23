using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.activate", 1)]
public sealed record ActivateTenant(Uuid TenantId, Uuid FirstAdministratorUserId,
    string? FirstAdministratorEmail = null) : IRequest;
