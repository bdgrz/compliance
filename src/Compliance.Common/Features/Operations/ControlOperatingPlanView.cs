using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One version of a control's operating plan: owner, backup owner, reviewer, cadence, and the
///     expected evidence copied from the exact approved control version. Status is
///     pending_approval, approved, or superseded.
/// </summary>
public sealed record ControlOperatingPlanView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid PlanVersionId, long Revision, string Status, Uuid ControlVersionId,
    OperatingHolder Owner, OperatingHolder? BackupOwner, Uuid ReviewerMemberId,
    ControlCadence Cadence, string CadenceDescription, IReadOnlyList<string> ExpectedEvidence,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil, string Rationale, Uuid ProposerMemberId,
    ActorReference ProposedBy, DateTimeOffset ProposedAt,
    Uuid? ProposalSeparationOfDutiesWaiverId = null, ActorReference? ApprovedBy = null,
    DateTimeOffset? ApprovedAt = null, string? ApprovalRationale = null,
    Uuid? ApprovalSeparationOfDutiesWaiverId = null,
    IReadOnlyList<OccurrenceReassignmentView>? Reassignments = null);
