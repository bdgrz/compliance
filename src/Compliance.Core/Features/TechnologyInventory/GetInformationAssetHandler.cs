using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class GetInformationAssetHandler(IAggregateReader aggregates,
    TechnologyInventoryReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<GetInformationAsset, InformationAssetView>
{
    public async ValueTask<Result<InformationAssetView>> HandleAsync(
        IRequestContext<GetInformationAsset> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<InformationAssetView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum information asset revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await aggregates.HydrateAsync(new InformationAsset(request.TenantId,
            request.InformationAssetId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadAssetAsync(request.TenantId, userId,
                source.Id, source.Content?.Classification, ct).ConfigureAwait(false))
            return Result<InformationAssetView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The information asset was not found."));
        var asset = await consistency.GetAssetAsync(request.TenantId, request.InformationAssetId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!asset.IsSuccess)
            return asset;
        if (!await visibility.CanReadAssetAsync(request.TenantId, userId, asset.Value, ct)
                .ConfigureAwait(false))
            return Result<InformationAssetView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The information asset was not found."));
        return asset;
    }
}
