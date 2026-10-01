using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     A non-human identity's governed terms. <c>OwnerKind</c> is <c>person</c> or <c>team</c>
///     and <c>OwnerId</c> names exactly one accountable owner. <c>ExpiresOn</c> is an optional date
///     from which the identity must no longer be used; an active identity cannot already be expired.
/// </summary>
public sealed record ServiceIdentityTerms(string DisplayName, string IdentityKind, string Purpose,
    string? Environment, string LifecycleStatus, string OwnerKind, Uuid OwnerId, DateOnly ReviewBy,
    DateOnly? ExpiresOn = null);
