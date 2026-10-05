using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.plan.version-defined", 1)]
public sealed record ControlEvaluationPlanVersionDefined(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid ControlVersionId, Uuid PlanVersionId, long Version, string Objective,
    IReadOnlyList<EvaluationProcedureStep> Steps, bool TesterIndependenceRequired,
    Uuid AuthoredByMemberId, ActorReference Author, DateTimeOffset DefinedAt) : DomainEvent;
