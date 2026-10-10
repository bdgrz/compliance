using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A frozen item with its decision history and remediation. <c>Status</c> is
///     <c>unresolved</c> until decided. <c>RemediationStatus</c> is <c>not_required</c>,
///     <c>pending</c>, <c>provider_changed</c>, <c>verified</c>, or <c>excepted</c>.
/// </summary>
public sealed record AccessReviewItemStateView(AccessReviewItemView Item, string Status,
    AccessDecisionView? Decision, IReadOnlyList<AccessDecisionView> DecisionHistory,
    string RemediationStatus, IReadOnlyList<AccessRemediationChangeView> ProviderChanges,
    AccessRemediationVerificationView? Verification, AccessRemediationExceptionView? Exception,
    Uuid? CurrentReviewerMemberId = null, Uuid? CurrentRemediationOwnerMemberId = null,
    IReadOnlyList<AccessReviewResponsibilityReassignmentView>? ResponsibilityReassignments = null);
