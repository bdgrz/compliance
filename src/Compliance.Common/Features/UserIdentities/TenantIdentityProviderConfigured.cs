using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the TenantIdentityProviderConfigured domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-configured", 1)]
public sealed record TenantIdentityProviderConfigured(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
