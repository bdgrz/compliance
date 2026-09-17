using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantMembershipSummary(Uuid TenantId, string Name, string Slug);
