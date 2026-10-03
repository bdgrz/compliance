using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

sealed class FitzReadinessReadModel(IReadinessDirectoryReader directory,
    ReadinessProjectionReadConsistency consistency) : IReadinessReadModel
{
    public ValueTask<Result<ReadinessAssessmentView>> GetAssessmentAsync(
        GetReadinessAssessment request, CancellationToken ct) => ExecuteAsync(request.TenantId,
        async token =>
        {
            var assessment = await directory.GetAssessmentAsync(request.TenantId,
                request.ProgramId, request.AssessmentId, token).ConfigureAwait(false);
            return assessment is null
                ? Result<ReadinessAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The readiness assessment was not found."))
                : Result<ReadinessAssessmentView>.Success(assessment);
        }, ct);

    public ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(
        ListReadinessAssessments request, CancellationToken ct) => ExecuteAsync(request.TenantId,
        token => directory.ListAssessmentsAsync(request.TenantId, request.ProgramId,
            request.Limit, request.Cursor, token), ct);

    public ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(ListReadinessGaps request,
        CancellationToken ct) => ExecuteAsync(request.TenantId, token => directory.ListGapsAsync(
        request.TenantId, request.ProgramId, request.AssessmentId, request.PlanState,
        request.Limit, request.Cursor, request.OwnerMemberId, request.Kind, request.RuleId,
        request.Subject, token), ct);

    public ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(
        ListReadinessAnnotations request, CancellationToken ct) => ExecuteAsync(request.TenantId,
        token => directory.ListAnnotationsAsync(request.TenantId, request.ProgramId,
            request.AssessmentId, request.Limit, request.Cursor, token), ct);

    public ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(
        ListTypeIEntryDecisions request, CancellationToken ct) => ExecuteAsync(request.TenantId,
        token => directory.ListTypeIEntryDecisionsAsync(request.TenantId, request.ProgramId,
            request.Limit, request.Cursor, token), ct);

    async ValueTask<Result<T>> ExecuteAsync<T>(Uuid tenantId,
        Func<CancellationToken, ValueTask<Result<T>>> query, CancellationToken ct)
    {
        var captured = await consistency.CaptureReadinessDirectoryAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<T>.Failure(captured.Error);

        var result = await query(ct).ConfigureAwait(false);
        var confirmed = await consistency.ConfirmReadinessDirectoryUnchangedAndCaughtUpAsync(
            tenantId, captured.Value, ct).ConfigureAwait(false);
        return confirmed.IsSuccess ? result : Result<T>.Failure(confirmed.Error);
    }
}
