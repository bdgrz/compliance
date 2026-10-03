using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public sealed class GetReadinessAssessmentHandler(IReadinessReadModel readModel)
    : IRequestHandler<GetReadinessAssessment, ReadinessAssessmentView>
{
    public ValueTask<Result<ReadinessAssessmentView>> HandleAsync(
        IRequestContext<GetReadinessAssessment> context, CancellationToken ct) =>
        readModel.GetAssessmentAsync(context.Request, ct);
}
