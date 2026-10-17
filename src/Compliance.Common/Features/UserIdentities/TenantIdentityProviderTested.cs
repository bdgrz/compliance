using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the TenantIdentityProviderTested domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-tested", 1)]
public sealed record TenantIdentityProviderTested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
