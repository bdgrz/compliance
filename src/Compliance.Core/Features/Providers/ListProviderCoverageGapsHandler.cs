using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ListProviderCoverageGapsHandler(IAssuranceReader directory,
    AssuranceReferences references, AssuranceReadConsistency consistency,
    AssuranceDisclosure disclosure)
    : IRequestHandler<ListProviderCoverageGaps, Page<ProviderCoverageGapView>>
{
    public async ValueTask<Result<Page<ProviderCoverageGapView>>> HandleAsync(
        IRequestContext<ListProviderCoverageGaps> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProviderCoverageGapView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The provider coverage gap list limit must be between 1 and 200."));
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
            .ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<Page<ProviderCoverageGapView>>.Failure(provider.Error);
        var fence = await consistency.CaptureFenceAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<ProviderCoverageGapView>>.Failure(fence.Error);
        Page<ProviderCoverageGapView> page;
        try
        {
            page = await directory.ListCoverageGapsAsync(request.TenantId,
                request.ProviderId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProviderCoverageGapView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The provider coverage gap cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.ProviderId != request.ProviderId))
            return Result<Page<ProviderCoverageGapView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The assurance projection is incomplete."));
        var unchanged = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!unchanged.IsSuccess)
            return Result<Page<ProviderCoverageGapView>>.Failure(unchanged.Error);
        if (await disclosure.CanReadRestrictedAsync(context, ct).ConfigureAwait(false))
            return Result<Page<ProviderCoverageGapView>>.Success(page);
        return Result<Page<ProviderCoverageGapView>>.Success(page with
        {
            Items = page.Items.Select(ProviderCoverageGapRules.Redact).ToArray(),
        });
    }
}
