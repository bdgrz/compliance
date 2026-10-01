using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.step_recorded", 1)]
public sealed record ControlEvaluationStepRecorded(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, Uuid StepId, EvaluationStepResultView Result,
    EvaluationDeviationView? Deviation) : DomainEvent;
