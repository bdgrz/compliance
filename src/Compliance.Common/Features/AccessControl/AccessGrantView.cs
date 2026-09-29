using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record AccessGrantView(
    Uuid TenantId,
    Uuid GrantId,
    AccessGrantTerms Terms,
    ActorReference? RevokedBy,
    DateTimeOffset? RevokedAt);
