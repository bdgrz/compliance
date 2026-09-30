using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
/// Reads the caught-up inventory projection and reports which active flows a reclassification or
/// retirement would change. The flow scan is bounded; it emits no event.
/// </summary>
public sealed class PreviewInformationAssetChangeHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency)
    : IRequestHandler<PreviewInformationAssetChange, InformationAssetChangePreview>
{
    public const int FlowScanLimit = 1000;

    public async ValueTask<Result<InformationAssetChangePreview>> HandleAsync(
        IRequestContext<PreviewInformationAssetChange> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectedRevision < 1)
            return Failure(RequestErrorKind.Validation, "The expected revision must be positive.");
        if (request.Classification is { } proposed &&
            !TechnologyInventoryRules.IsClassification(proposed.Trim()))
            return Failure(RequestErrorKind.Validation,
                "The classification must be public, internal, confidential, or restricted.");
        if (request.Lifecycle is { } lifecycle && lifecycle.Trim() is not
                (TechnologyInventoryRules.Active or TechnologyInventoryRules.Retired))
            return Failure(RequestErrorKind.Validation, "The lifecycle must be active or retired.");
        var asset = await consistency.GetAssetAsync(request.TenantId,
            request.InformationAssetId, null, ct).ConfigureAwait(false);
        if (!asset.IsSuccess)
            return Result<InformationAssetChangePreview>.Failure(asset.Error);
        if (asset.Value.Revision != request.ExpectedRevision)
            return Failure(RequestErrorKind.Conflict,
                $"The information asset is at revision {asset.Value.Revision}.");
        var flowsCaughtUp = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!flowsCaughtUp.IsSuccess)
            return Result<InformationAssetChangePreview>.Failure(flowsCaughtUp.Error);

        var current = asset.Value.Content;
        var proposedClassification = request.Classification?.Trim() ?? current.Classification;
        var proposedLifecycle = request.Lifecycle?.Trim() ?? current.Lifecycle;
        var affected = new List<DataFlowImpactView>();
        var assets = new Dictionary<Uuid, InformationAssetContent>();
        string? cursor = null;
        var scanned = 0;
        var overLimit = false;
        do
        {
            var page = await directory.ListFlowsAsync(request.TenantId, 200, cursor, ct)
                .ConfigureAwait(false);
            foreach (var flow in page.Items)
            {
                if (scanned++ == FlowScanLimit)
                {
                    overLimit = true;
                    break;
                }
                if (flow.TenantId != request.TenantId ||
                    flow.Content.Lifecycle != TechnologyInventoryRules.Active ||
                    !flow.Content.InformationAssetIds.Contains(request.InformationAssetId))
                    continue;
                foreach (var assetId in flow.Content.InformationAssetIds)
                    if (assetId != request.InformationAssetId && !assets.ContainsKey(assetId) &&
                        await directory.GetAssetAsync(request.TenantId, assetId, ct)
                            .ConfigureAwait(false) is { } other && other.TenantId == request.TenantId)
                        assets[assetId] = other.Content;
                affected.Add(InformationAssetImpact.Evaluate(flow, request.InformationAssetId,
                    proposedClassification, proposedLifecycle, assets));
            }
            cursor = page.NextCursor;
        } while (cursor is not null && !overLimit);

        return Result<InformationAssetChangePreview>.Success(new InformationAssetChangePreview(
            request.TenantId, request.InformationAssetId, asset.Value.Revision,
            current.Classification, proposedClassification, current.Lifecycle,
            proposedLifecycle, affected, overLimit));
    }

    static Result<InformationAssetChangePreview> Failure(RequestErrorKind kind, string message) =>
        Result<InformationAssetChangePreview>.Failure(new RequestError(kind, message));
}
