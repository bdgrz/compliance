using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class RecordProviderReviewHandler(IAggregateExecutor executor, IAggregateReader reader,
    AssuranceReferences references, TimeProvider clock)
    : IRequestHandler<RecordProviderReview, ProviderReviewRegistration>
{
    public async ValueTask<Result<ProviderReviewRegistration>> HandleAsync(
        IRequestContext<RecordProviderReview> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct).ConfigureAwait(false);
        if (source.CheckReviewRetry(context.RequestId, request.ProviderId, context.RequestId, request.Content) is { } retry)
            return retry;
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderReviewRegistration>.Failure(provider.Error);
        var artifact = await references.ArtifactAsync(request.TenantId, request.Content.Evidence, ct).ConfigureAwait(false);
        if (!artifact.IsSuccess)
            return Result<ProviderReviewRegistration>.Failure(artifact.Error);
        // The reviewer is always the authenticated member; a review is a personal sign-off.
        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderAssuranceRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.RecordReview(context.RequestId, request.ProviderId,
                context.RequestId, request.Content, provider.Value.Revision, provider.Value.Content.Materiality,
                actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
