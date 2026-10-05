using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlEvaluationWorkState(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, Uuid EvaluatorMemberId, long Revision, int Round, string State,
    DateTimeOffset? SubmittedAt);
