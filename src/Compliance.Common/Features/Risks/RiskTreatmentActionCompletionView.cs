using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     One submission that a treatment action is done, with the fulfilled evidence requests it
///     rests on and, once decided, the independent review. <c>ReviewOutcome</c> is <c>accept</c>
///     or <c>reject</c>; it is null while the review is pending.
/// </summary>
public sealed record RiskTreatmentActionCompletionView(Uuid SubmissionId, string Summary,
    IReadOnlyList<Uuid> EvidenceRequestIds, ActorReference SubmittedBy,
    DateTimeOffset SubmittedAt, Uuid? ReviewDecisionId, string? ReviewOutcome,
    ActorReference? ReviewedBy, string? ReviewRationale, DateTimeOffset? ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId);
