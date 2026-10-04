using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Applies explicit restricted-read permissions to information assets and data flows.</summary>
public sealed class TechnologyInventoryRestrictedVisibility(IPermissionAuthorizer permissions,
    IAccessGrantScopePermissionAuthorizer scopedPermissions)
{
    public ValueTask<bool> CanReadAssetAsync(Uuid tenantId, Uuid userId,
        InformationAssetView asset, CancellationToken ct = default) =>
        CanReadAsync(tenantId, userId, asset?.TenantId ?? Uuid.Empty,
            asset?.InformationAssetId ?? Uuid.Empty, asset?.Content?.Classification,
            TechnologyInventoryResourceTypes.InformationAsset, ct);

    public ValueTask<bool> CanReadAssetAsync(Uuid tenantId, Uuid userId, Uuid assetId,
        string? classification, CancellationToken ct = default) =>
        CanReadAsync(tenantId, userId, tenantId, assetId, classification,
            TechnologyInventoryResourceTypes.InformationAsset, ct);

    public ValueTask<bool> CanReadFlowAsync(Uuid tenantId, Uuid userId,
        DataFlowView flow, CancellationToken ct = default) =>
        CanReadAsync(tenantId, userId, flow?.TenantId ?? Uuid.Empty,
            flow?.DataFlowId ?? Uuid.Empty, flow?.Content?.Classification,
            TechnologyInventoryResourceTypes.DataFlow, ct);

    public ValueTask<bool> CanReadFlowAsync(Uuid tenantId, Uuid userId, Uuid flowId,
        string? classification, CancellationToken ct = default) =>
        CanReadAsync(tenantId, userId, tenantId, flowId, classification,
            TechnologyInventoryResourceTypes.DataFlow, ct);

    async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid userId, Uuid viewTenantId,
        Uuid resourceId, string? classification, string resourceType, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty || viewTenantId != tenantId || resourceId == Uuid.Empty ||
            !TechnologyInventoryRules.IsClassification(classification))
            return false;
        var memberId = RbacIds.Member(tenantId, userId);
        var scope = new AccessGrantScope(AccessGrantScopeKind.SharedResource, resourceId,
            resourceType);
        if (!await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TechnologyInventoryManage, ct).ConfigureAwait(false) &&
            !await scopedPermissions.IsAllowedAtAnyScopeAsync(tenantId, userId, memberId,
                [scope], RbacPermissions.TechnologyInventoryManage, ct).ConfigureAwait(false))
            return false;
        if (classification != "restricted")
            return true;
        if (await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TechnologyInventoryRestrictedRead, ct).ConfigureAwait(false))
            return true;

        return await scopedPermissions.IsAllowedAtAnyScopeAsync(tenantId, userId, memberId,
            [scope], RbacPermissions.TechnologyInventoryRestrictedRead, ct)
            .ConfigureAwait(false);
    }
}
