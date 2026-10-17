using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the TenantIdentityProviderCredentialsRotated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-credentials-rotated", 1)]
public sealed record TenantIdentityProviderCredentialsRotated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
