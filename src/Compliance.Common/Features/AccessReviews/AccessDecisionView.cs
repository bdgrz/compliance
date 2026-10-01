using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An attributable reviewer decision: <c>keep</c>, <c>modify</c>, <c>revoke</c>, or
///     <c>unable_to_determine</c>. A bulk decision shares one rationale and preview token.
/// </summary>
public sealed record AccessDecisionView(Uuid DecisionId, Uuid ItemId, string Decision,
    string Rationale, Uuid ReviewerMemberId, ActorReference DecidedBy, DateTimeOffset DecidedAt,
    string? BulkPreviewToken, bool AfterDeadline, Uuid? SeparationOfDutiesWaiverId);
