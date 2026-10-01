using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.criterion_applicability.get", 1)]
public sealed record GetCriterionApplicability(Uuid TenantId, Uuid ProgramId, Uuid DecisionId)
    : IRequest<CriterionApplicabilityView>, IProgramReadRequest, ICallable;
