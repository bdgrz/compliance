using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the TenantIdentityProviderDisabled domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-disabled", 1)]
public sealed record TenantIdentityProviderDisabled(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
