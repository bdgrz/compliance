using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantMembershipView(Uuid UserId, Uuid TenantId);
