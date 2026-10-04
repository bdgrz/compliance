using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class RecordProviderCoverageGapHandler(IAggregateExecutor executor,
    IAggregateReader reader, AssuranceReferences references, TimeProvider clock)
    : IRequestHandler<RecordProviderCoverageGap, ProviderCoverageGapRegistration>
{
    public async ValueTask<Result<ProviderCoverageGapRegistration>> HandleAsync(
        IRequestContext<RecordProviderCoverageGap> context, CancellationToken ct)
    {
        var request = context.Request;
        var gapId = context.RequestId;
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct)
            .ConfigureAwait(false);
        if (source.CheckCoverageGapRetry(gapId, request.ProviderId, context.RequestId,
                request.Content) is { } retry)
            return retry;
        if (ProviderCoverageGapRules.InputError(request.Content) is { } inputError)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, inputError));
        var content = ProviderCoverageGapRules.Normalize(request.Content);

        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
            .ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderCoverageGapRegistration>.Failure(provider.Error);
        var sourceReference = await references.CoverageGapSourceAsync(request.TenantId,
            provider.Value, content.ServiceId, content.SourceKind,
            content.SourceId, content.SourceRevision, ct).ConfigureAwait(false);
        if (!sourceReference.IsSuccess)
            return Result<ProviderCoverageGapRegistration>.Failure(sourceReference.Error);

        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderAssuranceRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.RecordCoverageGap(gapId,
                request.ProviderId, context.RequestId, provider.Value.Revision, content,
                actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
