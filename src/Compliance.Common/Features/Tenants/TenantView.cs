using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantView(Uuid TenantId, string Name, string Slug, string Status = "active",
    string? LegalName = null, Uuid? OperatorUserId = null, bool RequiresInvitation = false,
    bool RequiresActivation = false);
