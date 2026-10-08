using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Records a minor deviation's disposition: corrected, or accepted_with_waiver naming an approved waiver.</summary>
[Discriminator("bdgrz.control.evaluation.deviation.dispose", 1)]
public sealed record DisposeControlEvaluationDeviation(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid EvaluationId, Uuid DeviationId, long ExpectedRevision,
    string Disposition, string Rationale, Uuid? WaiverId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, ICallable;
