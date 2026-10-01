using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.submitted", 1)]
public sealed record ControlEvaluationSubmitted(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, EvaluationSubmissionView Submission) : DomainEvent;
