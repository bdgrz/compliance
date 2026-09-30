using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Retires one registered system instance without removing its scope history.</summary>
public sealed class RetireSystemInstanceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RetireSystemInstance>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireSystemInstance> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return await executor.ExecuteAsync(new DeclaredSystemInstance(request.TenantId,
                request.SystemInstanceId),
            instance => instance.IsCreated && instance.ApplicationId != request.ApplicationId
                ? AggregateOutcome.Discard(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The system instance was not found.")))
                : CommandFailureRequestAdapter.ToOutcome(instance.Retire(
                    request.ExpectedRevision, request.EffectiveAt, request.Reason,
                    memberId, display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
