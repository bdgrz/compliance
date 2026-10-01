using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Routes a submitted material deviation to a finding with a corrective action. Dispatched
///     only by the evaluation reactor; the finding ID is derived from the deviation.
/// </summary>
[Discriminator("bdgrz.finding.raise_from_evaluation_deviation", 1)]
public sealed record RaiseEvaluationDeviationFinding(Uuid TenantId, Uuid ProgramId,
    Uuid FindingId, Uuid ControlId, Uuid EvaluationId, Uuid DeviationId)
    : IRequest, IOperationsReactionRequest, ICallable;
