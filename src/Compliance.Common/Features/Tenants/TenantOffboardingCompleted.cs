using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Records the TenantOffboardingCompleted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.tenants.tenant-offboarding-completed", 1)]
public sealed record TenantOffboardingCompleted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
