using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlOperatingPlanApprovalWorkState(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid PlanVersionId, Uuid ControlVersionId, long Revision,
    Uuid ProposerMemberId, DateTimeOffset ProposedAt);
