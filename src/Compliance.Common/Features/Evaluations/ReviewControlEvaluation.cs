using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     The independent reviewer's personal decision on the submitted round; HTTP-only. Only a
///     program manager who is not the evaluator may review, unless an approved waiver applies.
/// </summary>
[Discriminator("bdgrz.control.evaluation.review", 1)]
public sealed record ReviewControlEvaluation(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long ExpectedRevision, string Decision, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, ICallable;
