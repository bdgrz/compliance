using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the propose type iioperating plan intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.operations.propose-type-iioperating-plan", 1)]
public sealed record ProposeTypeIIOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid PlanId, Uuid ExaminationId, DateOnly PeriodStart, DateOnly PeriodEnd, string Cadence, IReadOnlyList<Uuid> ControlVersionIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
