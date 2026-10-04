using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListInformationAssetBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<ListInformationAssetBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListInformationAssetBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (InventoryBoundaryReferenceReader.RejectLimit(request.Limit, "information asset")
            is { } rejected)
            return rejected;
        var asset = await aggregates.HydrateAsync(new InformationAsset(
            request.TenantId, request.InformationAssetId), ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var canRead = asset.IsCreated && await visibility.CanReadAssetAsync(request.TenantId,
            userId, asset.Id, asset.Content?.Classification, ct).ConfigureAwait(false);
        if (!canRead)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The information asset was not found."));
        return await InventoryBoundaryReferenceReader.ListAsync(true,
            "information asset", "information", request.TenantId, request.InformationAssetId,
            request.Limit, request.Cursor, directory, consistency, ct).ConfigureAwait(false);
    }
}
