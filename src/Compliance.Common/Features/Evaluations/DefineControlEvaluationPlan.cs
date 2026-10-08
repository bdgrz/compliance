using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.plan.define", 1)]
public sealed record DefineControlEvaluationPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, long ExpectedVersion, string Objective,
    IReadOnlyList<EvaluationProcedureStep>? Steps,
    bool TesterIndependenceRequired)
    : IRequest<ControlEvaluationPlanVersionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
