namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Exactly what the source asserts is granted directly to one principal. Inherited access is
///     never recorded as an assignment.
/// </summary>
public sealed record AccessAssignmentFact(string PrincipalProviderSubjectId,
    string ProviderEntitlementId, DateTimeOffset? ExpiresAt = null);
