using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A frozen reviewer assignment; <c>Delegated</c> is true when the reviewer is not the access owner.</summary>
public sealed record AccessReviewerView(Uuid PopulationId, Uuid SystemInstanceId,
    Uuid ReviewerMemberId, bool Delegated, string? DelegationReason);
