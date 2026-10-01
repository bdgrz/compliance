using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.operating_plan.approved", 1)]
public sealed record ControlOperatingPlanApproved(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, Uuid PlanVersionId, Uuid ApproverMemberId, ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt, string Rationale, Uuid? SeparationOfDutiesWaiverId,
    IReadOnlyList<OccurrenceReassignmentView> Reassignments) : DomainEvent;
