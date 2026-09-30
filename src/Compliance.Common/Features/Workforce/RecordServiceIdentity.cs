using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Records an active non-human identity with exactly one accountable owner (a recorded person
///     or an active team), an approved purpose, and a review date at most one year out (M0-D06).
///     The request ID becomes the service identity ID.
/// </summary>
[Discriminator("bdgrz.workforce.service-identity.record", 1)]
public sealed record RecordServiceIdentity(Uuid TenantId, string DisplayName, string IdentityKind,
    string Purpose, string OwnerKind, Uuid OwnerId, DateOnly ReviewBy, string? Environment = null)
    : IRequest<ServiceIdentityRegistration>, IWorkforceRequest, ICallable;
