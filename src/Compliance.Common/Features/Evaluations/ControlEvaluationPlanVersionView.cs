using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

public sealed record ControlEvaluationPlanVersionView(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid ControlVersionId, Uuid PlanVersionId, long Version, string Objective,
    IReadOnlyList<EvaluationProcedureStep> Steps, bool TesterIndependenceRequired,
    Uuid AuthoredByMemberId, ActorReference Author, DateTimeOffset DefinedAt);
