using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An approved exception for an in-scope system instance that has no accepted population.
///     It is effective from approval until <c>ExpiresAt</c>.
/// </summary>
public sealed record AccessPopulationExceptionView(Uuid ExceptionId, Uuid SystemInstanceId,
    string Reason, DateTimeOffset ExpiresAt, ActorReference ApprovedBy, DateTimeOffset ApprovedAt);
