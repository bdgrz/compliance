using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.operating_plan.get", 1)]
public sealed record GetControlOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId)
    : IRequest<ControlOperatingPlanSetView>, IProgramReadRequest, ICallable;
