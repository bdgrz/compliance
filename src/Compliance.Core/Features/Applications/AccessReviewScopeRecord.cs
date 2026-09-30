using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The projected, ordered scope history of one system instance.</summary>
public sealed record AccessReviewScopeRecord(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, IReadOnlyList<AccessReviewScopeDecisionView> Decisions);
