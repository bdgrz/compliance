using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.reviewed", 1)]
public sealed record ControlEvaluationReviewed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, ControlEvaluationReviewView Review) : DomainEvent;
