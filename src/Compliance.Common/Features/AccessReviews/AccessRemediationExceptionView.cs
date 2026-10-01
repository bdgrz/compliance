using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An approved exception that lets the campaign complete without verified remediation.</summary>
public sealed record AccessRemediationExceptionView(Uuid ExceptionId, string Rationale,
    DateTimeOffset? ExpiresAt, ActorReference ApprovedBy, DateTimeOffset ApprovedAt);
