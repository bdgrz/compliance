using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An attributable replacement for one launch-frozen responsibility.</summary>
public sealed record AccessReviewResponsibilityReassignmentView(Uuid ReassignmentId,
    string Responsibility, Uuid ItemId, Uuid PreviousMemberId, Uuid AssignedMemberId,
    string Reason, ActorReference ReassignedBy, DateTimeOffset ReassignedAt,
    string? DelegationReason = null);
