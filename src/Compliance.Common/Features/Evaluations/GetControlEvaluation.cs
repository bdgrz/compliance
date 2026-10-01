using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.get", 1)]
public sealed record GetControlEvaluation(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId)
    : IRequest<ControlEvaluationView>, IProgramReadRequest, ICallable;
