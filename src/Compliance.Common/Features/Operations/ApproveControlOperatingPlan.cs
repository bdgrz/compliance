using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The approver's personal decision to put a pending operating plan into effect; HTTP-only.
///     The proposer cannot approve without an approved waiver.
/// </summary>
[Discriminator("bdgrz.control.operating_plan.approve", 1)]
public sealed record ApproveControlOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, Uuid PlanVersionId, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlOperatingPlanView>, IProgramScopedRequest, ICallable;
