using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-owner.register", 1)]
public sealed record RegisterTenantOwner(Uuid TenantId, Uuid UserId) : IRequest;
