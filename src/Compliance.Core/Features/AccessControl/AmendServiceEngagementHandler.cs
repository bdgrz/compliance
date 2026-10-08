using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AmendServiceEngagementHandler(IAggregateExecutor executor, IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<AmendServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<AmendServiceEngagement> context, CancellationToken ct)
    {
        if (context.Request.Content is null)
            return ValueTask.FromResult(Result<ServiceEngagementView>.Failure(new RequestError(
                RequestErrorKind.Validation, "A service-engagement draft requires content.")));
        return EngagementStaffBoundary.ExecuteAsync(reader, executor, clock, context, context.Request.Content.EngagementLeadStaffMemberId,
            (ledger, staff, actor, recordedAt) => ledger.AmendEngagement(context.RequestId,
                context.Request.EngagementId, context.Request.ExpectedSequence, context.Request.Content, staff, actor, recordedAt), ct);
    }
}
