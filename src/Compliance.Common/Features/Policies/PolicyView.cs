using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     A policy record. <c>Status</c> is <c>draft</c> (never approved), <c>approved</c>, or
///     <c>retired</c>. <c>PendingStatus</c> describes the open proposal: <c>draft</c>,
///     <c>changes_requested</c>, <c>awaiting_approval</c>, <c>retirement_proposed</c>, or
///     <c>retirement_awaiting_approval</c>; it is null when nothing is pending.
/// </summary>
public sealed record PolicyView(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, string Status, string? PendingStatus, long Revision,
    PolicyContent? Draft, string? DraftContentSha256, long? DraftPredecessorVersion,
    Uuid? AcceptedReviewDecisionId, long? CurrentVersion, DateOnly? CurrentEffectiveFrom,
    DateOnly? LastReviewedOn, DateOnly? NextReviewDueOn, bool ReviewOverdue,
    PolicyRetirementProposalView? PendingRetirement, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
