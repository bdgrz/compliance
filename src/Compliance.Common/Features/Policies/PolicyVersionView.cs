using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     An immutable approved policy version. <c>Status</c> is <c>approved</c>,
///     <c>superseded</c>, or <c>retired</c>. Its half-open effective interval is closed only by a
///     successor or retirement.
/// </summary>
public sealed record PolicyVersionView(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, long Version, long Revision, bool Major, string Status,
    PolicyContent Content, string ContentSha256, DateOnly EffectiveFrom,
    DateOnly? EffectiveUntil, long? PredecessorVersion, Uuid ApprovalDecisionId,
    Uuid AcceptedReviewDecisionId, ActorReference ApprovedBy, DateTimeOffset ApprovedAt,
    string? ImpactDigest, Uuid? SeparationOfDutiesWaiverId);
