using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.decision.get", 1)]
public sealed record GetControlDecision(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid DecisionId)
    : IRequest<ControlDecisionView>, IProgramScopedRequest, ICallable;
