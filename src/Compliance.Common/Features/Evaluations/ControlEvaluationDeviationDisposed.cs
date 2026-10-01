using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.deviation_disposed", 1)]
public sealed record ControlEvaluationDeviationDisposed(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid EvaluationId, long Revision, Uuid DeviationId, string Disposition,
    string Rationale, Uuid? WaiverId) : DomainEvent;
