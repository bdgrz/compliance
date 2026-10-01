using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     A control design and implementation evaluation against one exact control version. State is
///     in_progress, submitted, or accepted. Retest status is not_required, required, in_progress,
///     passed, or failed. Every submission and review stays in history.
/// </summary>
public sealed record ControlEvaluationView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, Uuid ControlVersionId, string State, int Round,
    Uuid EvaluatorMemberId, ActorReference StartedBy, DateTimeOffset StartedAt,
    IReadOnlyList<ControlEvaluationStepView> Steps,
    IReadOnlyList<EvaluationDeviationView> Deviations,
    IReadOnlyList<EvaluationSubmissionView> Submissions,
    IReadOnlyList<ControlEvaluationReviewView> Reviews, ControlEvaluationReviewView? LatestReview,
    string? AcceptedOverall, Uuid? RetestOfEvaluationId, IReadOnlyList<Uuid> RetestOfDeviationIds,
    string RetestStatus, IReadOnlyList<Uuid> RetestEvaluationIds);
