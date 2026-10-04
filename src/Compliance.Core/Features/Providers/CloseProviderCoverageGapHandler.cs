using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Closes a provider gap only against a verified later source.</summary>
public sealed class CloseProviderCoverageGapHandler(IAggregateExecutor executor,
    IAggregateReader reader, AssuranceReferences references, TimeProvider clock)
    : IRequestHandler<CloseProviderCoverageGap, ProviderCoverageGapRegistration>
{
    public async ValueTask<Result<ProviderCoverageGapRegistration>> HandleAsync(
        IRequestContext<CloseProviderCoverageGap> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct)
            .ConfigureAwait(false);
        if (source.CheckCoverageGapClosureRetry(request.GapId, context.RequestId,
                request.ExpectedRevision, request.Content) is { } retry)
            return retry;
        if (ProviderCoverageGapRules.ClosureInputError(request.Content) is { } inputError)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, inputError));
        var content = ProviderCoverageGapRules.Normalize(request.Content);
        if (source.CoverageGap(request.GapId) is not { } gap ||
            gap.ProviderId != request.ProviderId || gap.TenantId != request.TenantId)
            return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The provider coverage gap was not found."));

        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
            .ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderCoverageGapRegistration>.Failure(provider.Error);
        var sourceReference = await references.CoverageGapSourceAsync(request.TenantId,
            provider.Value, gap.Content.ServiceId, content.SourceKind,
            content.SourceId, content.SourceRevision, ct).ConfigureAwait(false);
        if (!sourceReference.IsSuccess)
            return Result<ProviderCoverageGapRegistration>.Failure(sourceReference.Error);

        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderAssuranceRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.CloseCoverageGap(request.GapId,
                request.ProviderId, context.RequestId, request.ExpectedRevision, content,
                actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
