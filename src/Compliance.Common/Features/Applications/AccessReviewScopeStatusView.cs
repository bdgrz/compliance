using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// The readiness-facing scope status of one system instance at <see cref="AsOf"/>.
/// <see cref="Status"/> is <c>included</c>, <c>excluded</c>, or <c>unresolved</c>;
/// <see cref="ReviewOverdue"/> is true when the effective decision's review date has passed.
/// <see cref="Upcoming"/> is the next decision that takes effect after <see cref="AsOf"/>.
/// </summary>
public sealed record AccessReviewScopeStatusView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, string InstanceLifecycle, DateTimeOffset AsOf, string Status,
    bool ReviewOverdue, long DecisionCount, AccessReviewScopeDecisionView? Effective,
    AccessReviewScopeDecisionView? Upcoming);
