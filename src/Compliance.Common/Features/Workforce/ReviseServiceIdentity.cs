using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Replaces a non-human identity's terms, including its owner, review date, and
///     optional expiry date.</summary>
[Discriminator("bdgrz.workforce.service-identity.revise", 1)]
public sealed record ReviseServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId,
    long ExpectedRevision, string DisplayName, string IdentityKind, string Purpose,
    string OwnerKind, Uuid OwnerId, DateOnly ReviewBy, string LifecycleStatus,
    string? Environment = null, DateOnly? ExpiresOn = null) : IRequest, IWorkforceRequest, ICallable;
