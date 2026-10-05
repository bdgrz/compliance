using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.started", 1)]
public sealed record ControlEvaluationStarted(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, Uuid ControlVersionId,
    IReadOnlyList<EvaluationProcedureStep> Steps, Uuid EvaluatorMemberId, ActorReference StartedBy,
    DateTimeOffset StartedAt, Uuid? RetestOfEvaluationId, IReadOnlyList<Uuid> RetestOfDeviationIds)
    : DomainEvent
{
    /// <summary>The exact immutable procedure version used by new evaluations.</summary>
    public Uuid? PlanVersionId { get; init; }
    public long? PlanVersion { get; init; }
}
