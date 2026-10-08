using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class CreateServiceEngagementHandler(IAggregateExecutor executor, IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<CreateServiceEngagement> context, CancellationToken ct)
    {
        if (context.Request.Content is null)
            return ValueTask.FromResult(Result<ServiceEngagementView>.Failure(new RequestError(
                RequestErrorKind.Validation, "A service-engagement draft requires content.")));
        return EngagementStaffBoundary.ExecuteAsync(reader, executor, clock, context, context.Request.Content.EngagementLeadStaffMemberId,
            (ledger, staff, actor, recordedAt) => ledger.CreateEngagement(context.RequestId,
                context.Request.EngagementId, context.Request.ExpectedSequence, context.Request.Content, staff, actor, recordedAt), ct);
    }
}
