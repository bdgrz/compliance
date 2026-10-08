using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Proposes owner, backup owner, reviewer, cadence, and effective date for the control's exact
///     current approved version. The plan takes effect only after an independent approval.
/// </summary>
[Discriminator("bdgrz.control.operating_plan.propose", 1)]
public sealed record ProposeControlOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, Uuid ControlVersionId, OperatingHolder Owner,
    OperatingHolder? BackupOwner, Uuid ReviewerMemberId, ControlCadence Cadence,
    DateOnly EffectiveFrom, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlOperatingPlanView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
