using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// The decision effective at <see cref="AsOf"/> plus the full retained history. A status of
/// <c>unresolved</c> means no approved decision is effective yet.
/// </summary>
public sealed record AccessReviewScopeView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, DateTimeOffset AsOf, string Status,
    AccessReviewScopeDecisionView? Effective,
    IReadOnlyList<AccessReviewScopeDecisionView> Decisions);
