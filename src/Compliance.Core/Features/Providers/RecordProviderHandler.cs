using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class RecordProviderHandler(IAggregateExecutor executor, IAggregateReader reader,
    ProviderReferences references, TimeProvider clock) : IRequestHandler<RecordProvider, ProviderRegistration>
{
    public async ValueTask<Result<ProviderRegistration>> HandleAsync(IRequestContext<RecordProvider> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderRegister(request.TenantId), ct).ConfigureAwait(false);
        if (source.CheckRetry(context.RequestId, context.RequestId, null, request.Content) is { } retry)
            return retry;
        var resolved = await references.ResolveAsync(request.TenantId, request.Content, ct).ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result<ProviderRegistration>.Failure(resolved.Error);
        var actor = ProviderActor.From(context);
        // The executor rehydrates the owning stream and rechecks the retained request and name.
        // External revisions are historical observations; no cross-stream fence is claimed.
        return await executor.ExecuteAsync(new ProviderRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.Record(context.RequestId, context.RequestId,
                resolved.Value, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
