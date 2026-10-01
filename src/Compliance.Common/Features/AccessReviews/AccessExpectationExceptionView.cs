using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An approved, expiring exception to one expectation for one principal and optional entitlement.</summary>
public sealed record AccessExpectationExceptionView(Uuid ExceptionId, Uuid ExpectationId,
    string ProviderSubjectId, string? ProviderEntitlementId, string Rationale,
    DateTimeOffset ExpiresAt, ActorReference ApprovedBy, DateTimeOffset ApprovedAt);
