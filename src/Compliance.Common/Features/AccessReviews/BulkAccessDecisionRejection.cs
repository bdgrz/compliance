using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Why an item cannot be bulk-decided: <c>not_found</c>, <c>not_assigned</c>,
///     <c>self_review</c>, or <c>privileged_requires_individual_decision</c>.
/// </summary>
public sealed record BulkAccessDecisionRejection(Uuid ItemId, string Reason);
