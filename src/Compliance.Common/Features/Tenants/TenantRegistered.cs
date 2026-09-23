using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.registered", 1)]
public sealed record TenantRegistered(Uuid TenantId, Uuid OwnerUserId, string Name, string Slug,
    string? LegalName = null, string? FirstAdministratorEmail = null,
    bool CreatorIsAdministrator = false) : DomainEvent;
