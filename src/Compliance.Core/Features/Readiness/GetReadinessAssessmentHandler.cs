using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public sealed class GetReadinessAssessmentHandler(IAggregateReader reader)
    : IRequestHandler<GetReadinessAssessment, ReadinessAssessmentView>
{
    public async ValueTask<Result<ReadinessAssessmentView>> HandleAsync(
        IRequestContext<GetReadinessAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ledger.Read(request.AssessmentId) is { } view
            ? Result<ReadinessAssessmentView>.Success(view)
            : Result<ReadinessAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The readiness assessment was not found."));
    }
}
