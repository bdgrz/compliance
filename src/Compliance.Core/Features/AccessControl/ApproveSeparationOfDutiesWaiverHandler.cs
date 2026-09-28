using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ApproveSeparationOfDutiesWaiverHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ApproveSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>
{
    public ValueTask<Result<SeparationOfDutiesWaiverView>> HandleAsync(
        IRequestContext<ApproveSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var approverUserId))
            return ValueTask.FromResult(Result<SeparationOfDutiesWaiverView>.Failure(
                new RequestError(RequestErrorKind.Unauthorized,
                    "Waiver administration requires a Bdgrz user identity.")));
        var request = context.Request;
        var memberId = RbacIds.Member(request.TenantId, approverUserId);
        var display = UserIdentityClaims.BdgrzDisplay(context.Actor, approverUserId);
        var now = clock.GetUtcNow();
        return executor.ExecuteAsync(new SeparationOfDutiesWaiver(request.TenantId, request.WaiverId),
            waiver =>
            {
                var failure = waiver.Approve(memberId, display, now);
                return failure is null
                    ? AggregateOutcome.Commit(Result<SeparationOfDutiesWaiverView>.Success(
                        waiver.ToView(now)))
                    : CommandFailureRequestAdapter.ToOutcome<SeparationOfDutiesWaiverView>(
                        failure, default!);
            }, context, ct);
    }
}
