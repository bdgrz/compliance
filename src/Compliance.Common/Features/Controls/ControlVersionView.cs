using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     An immutable, approved control version. Activation does not prove criteria coverage,
///     risk treatment, collected evidence, control operation, or audit readiness.
/// </summary>
public sealed record ControlVersionView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, Uuid VersionId, long Revision, string Status, string ContentOrigin,
    ControlDraftContent Content, DateOnly EffectiveFrom, Uuid? PredecessorVersionId,
    Uuid OwnerAssignmentId, Uuid OwnerMemberId, string OwnerResolution,
    Uuid AcceptedReviewDecisionId, Uuid ApprovalDecisionId, ActorReference ApprovedBy,
    string ApprovalRationale, DateTimeOffset ApprovedAt,
    Uuid? SeparationOfDutiesWaiverId);
