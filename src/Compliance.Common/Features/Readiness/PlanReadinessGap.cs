using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Assigns an owner, target date, and planned action to a gap's stable identity.</summary>
[Discriminator("bdgrz.readiness.gap.plan", 1)]
public sealed record PlanReadinessGap(Uuid TenantId, Uuid ProgramId, Uuid GapId,
    long ExpectedRevision, Uuid OwnerMemberId, DateOnly TargetDate, string Action)
    : IRequest<ReadinessGapPlanView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
