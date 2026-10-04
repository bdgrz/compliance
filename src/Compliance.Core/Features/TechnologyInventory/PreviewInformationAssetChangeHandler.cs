using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
/// Reads the caught-up inventory projection and reports which active flows a reclassification or
/// retirement would change. The flow scan is bounded; it emits no event.
/// </summary>
public sealed class PreviewInformationAssetChangeHandler(IAggregateReader aggregates,
    ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility)
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
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await aggregates.HydrateAsync(new InformationAsset(request.TenantId,
            request.InformationAssetId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadAssetAsync(request.TenantId, userId,
                source.Id, source.Content?.Classification, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The information asset was not found.");
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<InformationAssetChangePreview>.Failure(fence.Error);
        var asset = await consistency.GetAssetAsync(request.TenantId,
            request.InformationAssetId, null, ct).ConfigureAwait(false);
        if (!asset.IsSuccess)
            return Result<InformationAssetChangePreview>.Failure(asset.Error);
        if (!await visibility.CanReadAssetAsync(request.TenantId, userId, asset.Value, ct)
                .ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The information asset was not found.");
        if (asset.Value.Revision != request.ExpectedRevision)
            return Failure(RequestErrorKind.Conflict,
                $"The information asset is at revision {asset.Value.Revision}.");
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
                if (flow.TenantId != request.TenantId || flow.DataFlowId == Uuid.Empty)
                    return Failure(RequestErrorKind.NotFound, "The data flow was not found.");
                if (!await visibility.CanReadFlowAsync(request.TenantId, userId, flow, ct)
                        .ConfigureAwait(false))
                    continue;
                if (scanned++ == FlowScanLimit)
                {
                    overLimit = true;
                    break;
                }
                if (flow.Content.Lifecycle != TechnologyInventoryRules.Active ||
                    !flow.Content.InformationAssetIds.Contains(request.InformationAssetId))
                    continue;
                foreach (var assetId in flow.Content.InformationAssetIds)
                {
                    if (assetId == request.InformationAssetId || assets.ContainsKey(assetId))
                        continue;
                    if (await directory.GetAssetAsync(request.TenantId, assetId, ct)
                            .ConfigureAwait(false) is not { } other || other.TenantId != request.TenantId)
                        continue;
                    if (await visibility.CanReadAssetAsync(request.TenantId, userId, other, ct)
                            .ConfigureAwait(false))
                        assets[assetId] = other.Content;
                }
                affected.Add(InformationAssetImpact.Evaluate(flow, request.InformationAssetId,
                    proposedClassification, proposedLifecycle, assets));
            }
            cursor = page.NextCursor;
        } while (cursor is not null && !overLimit);

        var unchanged = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!unchanged.IsSuccess)
            return Result<InformationAssetChangePreview>.Failure(unchanged.Error);

        return Result<InformationAssetChangePreview>.Success(new InformationAssetChangePreview(
            request.TenantId, request.InformationAssetId, asset.Value.Revision,
            current.Classification, proposedClassification, current.Lifecycle,
            proposedLifecycle, affected, overLimit));
    }

    static Result<InformationAssetChangePreview> Failure(RequestErrorKind kind, string message) =>
        Result<InformationAssetChangePreview>.Failure(new RequestError(kind, message));
}
