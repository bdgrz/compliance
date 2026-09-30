using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class GetInformationAssetHandler(TechnologyInventoryReadConsistency consistency)
    : IRequestHandler<GetInformationAsset, InformationAssetView>
{
    public ValueTask<Result<InformationAssetView>> HandleAsync(IRequestContext<GetInformationAsset> context,
        CancellationToken ct) =>
        consistency.GetAssetAsync(context.Request.TenantId, context.Request.InformationAssetId,
            context.Request.MinimumRevision, ct);
}
