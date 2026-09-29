using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record AccessGrantTerms(
    AccessGrantPrincipal Principal,
    Uuid RoleId,
    AccessGrantScope Scope,
    AccessGrantSource Source,
    ActorReference GrantedBy,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil);
