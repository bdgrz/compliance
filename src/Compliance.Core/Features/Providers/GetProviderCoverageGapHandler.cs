using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class GetProviderCoverageGapHandler(IAssuranceReader directory,
    AssuranceReferences references, AssuranceReadConsistency consistency,
    AssuranceDisclosure disclosure)
    : IRequestHandler<GetProviderCoverageGap, ProviderCoverageGapView>
{
    public async ValueTask<Result<ProviderCoverageGapView>> HandleAsync(
        IRequestContext<GetProviderCoverageGap> context, CancellationToken ct)
    {
        var request = context.Request;
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
            .ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderCoverageGapView>.Failure(provider.Error);
        var fence = await consistency.CaptureFenceAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ProviderCoverageGapView>.Failure(fence.Error);
        var gap = await directory.GetCoverageGapAsync(request.TenantId, request.GapId, ct)
            .ConfigureAwait(false);
        if (gap is null || gap.ProviderId != request.ProviderId || gap.TenantId != request.TenantId)
            return Result<ProviderCoverageGapView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The provider coverage gap was not found."));
        var unchanged = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!unchanged.IsSuccess)
            return Result<ProviderCoverageGapView>.Failure(unchanged.Error);
        return await disclosure.CanReadRestrictedAsync(context, ct).ConfigureAwait(false)
            ? Result<ProviderCoverageGapView>.Success(gap)
            : Result<ProviderCoverageGapView>.Success(ProviderCoverageGapRules.Redact(gap));
    }
}
