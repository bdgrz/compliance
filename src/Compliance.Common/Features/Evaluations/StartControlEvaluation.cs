using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Starts an evaluation of the control's current approved version and freezes its procedure.
///     A retest names the accepted evaluation and reuses its procedure when none is given.
/// </summary>
[Discriminator("bdgrz.control.evaluation.start", 1)]
public sealed record StartControlEvaluation(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    IReadOnlyList<EvaluationProcedureStep>? Steps = null, Uuid? RetestOfEvaluationId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, ICallable;
