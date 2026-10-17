using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.programs.get-next-period-plan", 1)]
public sealed record GetNextPeriodPlan(Uuid TenantId, Uuid ProgramId, Uuid NextPeriodPlanId)
    : IRequest<NextPeriodPlanView>, IProgramReadRequest, ICallable;
