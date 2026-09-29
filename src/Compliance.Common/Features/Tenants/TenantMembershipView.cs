using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantMembershipView(Uuid UserId, Uuid TenantId,
    string Affiliation = "client_personnel", bool IsSuspended = false,
    DateTimeOffset? SuspendedAt = null, Uuid SuspendedByMemberId = default,
    string? SuspendedByDisplay = null, string? SuspensionReason = null,
    DateTimeOffset? ReinstatedAt = null, Uuid ReinstatedByMemberId = default,
    string? ReinstatedByDisplay = null);
