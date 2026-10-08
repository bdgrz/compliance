using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class EngagementStaffBoundary
{
    internal static async ValueTask<Result<ServiceEngagementView>> ExecuteAsync<T>(IAggregateReader reader,
        IAggregateExecutor executor, TimeProvider clock, IRequestContext<T> context, Uuid staffMemberId,
        Func<IndependenceLedger, FirmStaffMemberView, ActorReference, DateTimeOffset, Result<ServiceEngagementView>> operation,
        CancellationToken ct) where T : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest
    {
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var observedSequence = directory.Sequence;
        if (directory.Get(staffMemberId) is not { IsActive: true } staff)
            return Result<ServiceEngagementView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The current active firm-staff identity was not found."));
        var result = await executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(operation(ledger, staff,
                AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())), context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        var latest = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        return latest.Sequence == observedSequence ? result : Result<ServiceEngagementView>.Failure(new RequestError(
            RequestErrorKind.Conflict, "The staff directory source changed. Reload before retaining another tentative proposal.",
            isTransient: true));
    }
}
