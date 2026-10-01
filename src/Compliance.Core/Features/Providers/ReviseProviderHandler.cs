using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ReviseProviderHandler(IAggregateExecutor executor, IAggregateReader reader,
    ProviderReferences references, TimeProvider clock) : IRequestHandler<ReviseProvider, ProviderRegistration>
{
    public async ValueTask<Result<ProviderRegistration>> HandleAsync(IRequestContext<ReviseProvider> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderRegister(request.TenantId), ct).ConfigureAwait(false);
        if (source.CheckRetry(request.ProviderId, context.RequestId, request.ExpectedRevision, request.Content) is { } retry)
            return retry;
        var current = source.Get(request.ProviderId);
        if (current is null)
            return Result<ProviderRegistration>.Failure(new RequestError(RequestErrorKind.NotFound, "The provider was not found."));
        if (request.ExpectedRevision != current.Revision)
            return Result<ProviderRegistration>.Failure(VersionedRecordRules.StaleRevision("provider", current.Revision).ToRequestError());
        var resolved = await references.ResolveAsync(request.TenantId, request.Content, ct).ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result<ProviderRegistration>.Failure(resolved.Error);
        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.Revise(request.ProviderId, context.RequestId,
                request.ExpectedRevision, resolved.Value, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
