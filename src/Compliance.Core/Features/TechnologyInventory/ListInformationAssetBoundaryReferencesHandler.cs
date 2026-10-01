using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListInformationAssetBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency)
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
        return await InventoryBoundaryReferenceReader.ListAsync(asset.IsCreated,
            "information asset", "information", request.TenantId, request.InformationAssetId,
            request.Limit, request.Cursor, directory, consistency, ct).ConfigureAwait(false);
    }
}
