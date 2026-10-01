using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One versioned expectation. <c>Status</c> is <c>proposed</c> or <c>approved</c>; an approved
///     successor ends its predecessor at the successor's effective date.
/// </summary>
public sealed record AccessExpectationView(Uuid ExpectationId, Uuid SystemInstanceId,
    Uuid? SupersedesExpectationId, string RuleKind, AccessExpectationParameters Parameters,
    string Rationale, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    string Status, ActorReference ProposedBy, DateTimeOffset ProposedAt,
    ActorReference? ApprovedBy, DateTimeOffset? ApprovedAt,
    Uuid? SeparationOfDutiesWaiverId);
