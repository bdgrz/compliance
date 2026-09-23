using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Creates an organization. FirstAdministratorEmail is retained for local developer
///     invitation fixtures; production selects the creator's verified email server-side.
/// </summary>
[Discriminator("bdgrz.tenant.register", 1)]
public sealed record RegisterTenant(string Name, string Slug, string? LegalName = null,
    string? FirstAdministratorEmail = null) : IRequest<TenantRegistration>, ICallable;
