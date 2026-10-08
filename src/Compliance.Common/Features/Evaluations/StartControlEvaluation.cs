using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Starts an evaluation against one exact immutable control-evaluation plan version. A retest
///     names the accepted evaluation and may use its plan version or a successor.
/// </summary>
[Discriminator("bdgrz.control.evaluation.start", 1)]
public sealed record StartControlEvaluation(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid PlanVersionId, Uuid? RetestOfEvaluationId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, ICallable;
