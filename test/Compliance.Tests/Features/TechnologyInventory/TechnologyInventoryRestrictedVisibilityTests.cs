using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class TechnologyInventoryRestrictedVisibilityTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid AssetId = Uuid.CreateVersion4();
    static readonly Uuid FlowId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldDenyRestrictedAssetGivenNoRestrictedReadAuthority()
    {
        // Arrange
        var grants = new ScopedPermissionAuthorizer();
        var visibility = Visibility(new PermissionAuthorizer(false), grants);

        // Act
        var allowed = await visibility.CanReadAssetAsync(TenantId, UserId,
            Asset("restricted"));

        // Assert
        Assert.False(allowed);
        Assert.Equal([new AccessGrantScope(AccessGrantScopeKind.SharedResource, AssetId,
            "information_asset")], grants.RequestedScopes);
    }

    [Fact]
    public async Task ShouldAllowRestrictedAssetGivenExactResourceGrant()
    {
        // Arrange
        var scope = new AccessGrantScope(AccessGrantScopeKind.SharedResource, AssetId,
            "information_asset");
        var grants = new ScopedPermissionAuthorizer(scope);
        var visibility = Visibility(new PermissionAuthorizer(false), grants);

        // Act
        var allowed = await visibility.CanReadAssetAsync(TenantId, UserId,
            Asset("restricted"));

        // Assert
        Assert.True(allowed);
        Assert.Contains(scope, grants.RequestedScopes);
    }

    [Fact]
    public async Task ShouldDenyRestrictedAssetGivenGrantForDifferentResourceType()
    {
        // Arrange
        var grants = new ScopedPermissionAuthorizer(new AccessGrantScope(
            AccessGrantScopeKind.SharedResource, AssetId, "data_flow"));
        var visibility = Visibility(new PermissionAuthorizer(false), grants);

        // Act
        var allowed = await visibility.CanReadAssetAsync(TenantId, UserId,
            Asset("restricted"));

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldAllowRestrictedAssetGivenOrganizationPermission()
    {
        // Arrange
        var grants = new ScopedPermissionAuthorizer();
        var permissions = new PermissionAuthorizer(true);
        var visibility = Visibility(permissions, grants);

        // Act
        var allowed = await visibility.CanReadAssetAsync(TenantId, UserId,
            Asset("restricted"));

        // Assert
        Assert.True(allowed);
        Assert.Equal(1, permissions.CallCount);
        Assert.Empty(grants.RequestedScopes);
    }

    [Fact]
    public async Task ShouldSkipRestrictedReadChecksGivenNonRestrictedAsset()
    {
        // Arrange
        var grants = new ScopedPermissionAuthorizer();
        var permissions = new PermissionAuthorizer(false);
        var visibility = Visibility(permissions, grants);

        // Act
        var allowed = await visibility.CanReadAssetAsync(TenantId, UserId,
            Asset("confidential"));

        // Assert
        Assert.True(allowed);
        Assert.Equal(0, permissions.CallCount);
        Assert.Empty(grants.RequestedScopes);
    }

    [Fact]
    public async Task ShouldRequireDataFlowResourceGrantGivenRestrictedDataFlow()
    {
        // Arrange
        var scope = new AccessGrantScope(AccessGrantScopeKind.SharedResource, FlowId,
            "data_flow");
        var grants = new ScopedPermissionAuthorizer(scope);
        var visibility = Visibility(new PermissionAuthorizer(false), grants);

        // Act
        var allowed = await visibility.CanReadFlowAsync(TenantId, UserId, Flow("restricted"));

        // Assert
        Assert.True(allowed);
        Assert.Contains(scope, grants.RequestedScopes);
    }

    [Fact]
    public async Task ShouldDenyForeignTenantViewGivenRestrictedAsset()
    {
        // Arrange
        var grants = new ScopedPermissionAuthorizer(new AccessGrantScope(
            AccessGrantScopeKind.SharedResource, AssetId, "information_asset"));
        var visibility = Visibility(new PermissionAuthorizer(true), grants);

        // Act
        var allowed = await visibility.CanReadAssetAsync(Uuid.CreateVersion4(), UserId,
            Asset("restricted"));

        // Assert
        Assert.False(allowed);
        Assert.Equal(0, grants.CallCount);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("internal")]
    [InlineData("confidential")]
    [InlineData("restricted")]
    public async Task ShouldHideUnrelatedRecordGivenOnlyAnExactAssetGrant(string classification)
    {
        // Arrange
        var granted = new AccessGrantScope(AccessGrantScopeKind.SharedResource,
            Uuid.CreateVersion4(), TechnologyInventoryResourceTypes.InformationAsset);
        var visibility = Visibility(new PermissionAuthorizer(false, manageAllowed: false),
            new ScopedPermissionAuthorizer(granted));

        // Act
        var allowed = await visibility.CanReadAssetAsync(TenantId, UserId, Asset(classification));

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("restricted")]
    public async Task ShouldKeepResourceTypesDistinctGivenAnAssetGrantWithTheFlowId(
        string classification)
    {
        // Arrange
        var grant = new AccessGrantScope(AccessGrantScopeKind.SharedResource, FlowId,
            TechnologyInventoryResourceTypes.InformationAsset);
        var visibility = Visibility(new PermissionAuthorizer(false, manageAllowed: false),
            new ScopedPermissionAuthorizer(grant));

        // Act
        var allowed = await visibility.CanReadFlowAsync(TenantId, UserId, Flow(classification));

        // Assert
        Assert.False(allowed);
    }

    static TechnologyInventoryRestrictedVisibility Visibility(
        PermissionAuthorizer permissions, ScopedPermissionAuthorizer grants) =>
        new(permissions, grants);

    static InformationAssetView Asset(string classification) => new(TenantId, AssetId, 1,
        new InformationAssetContent("Customer records", classification, "RET-1",
            Uuid.CreateVersion4(), null, "active"), "manual",
        ActorReference.ForMember(UserId, "Inventory Manager"), DateTimeOffset.UtcNow);

    static DataFlowView Flow(string classification) => new(TenantId, FlowId, 1,
        new DataFlowContent("component", Uuid.CreateVersion4(), "external_party", null,
            "Payroll service", [AssetId], "Payroll processing", true, true, null,
            new DateOnly(2026, 1, 1), Uuid.CreateVersion4(), "active", classification),
        "manual", ActorReference.ForMember(UserId, "Inventory Manager"), DateTimeOffset.UtcNow);

    sealed class PermissionAuthorizer(bool allowed, bool manageAllowed = true) : IPermissionAuthorizer
    {
        public int CallCount { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default)
        {
            if (permission == RbacPermissions.TechnologyInventoryRestrictedRead)
                CallCount++;
            Assert.Equal(TenantId, tenantId);
            Assert.Equal(UserId, userId);
            Assert.Equal(RbacIds.Member(TenantId, UserId), memberId);
            Assert.Contains(permission, new[] { RbacPermissions.TechnologyInventoryManage,
                RbacPermissions.TechnologyInventoryRestrictedRead });
            return ValueTask.FromResult(permission == RbacPermissions.TechnologyInventoryManage
                ? manageAllowed : allowed);
        }
    }

    sealed class ScopedPermissionAuthorizer(params AccessGrantScope[] allowedScopes)
        : IAccessGrantScopePermissionAuthorizer
    {
        public List<AccessGrantScope> RequestedScopes { get; } = [];
        public int CallCount { get; private set; }

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default)
        {
            CallCount++;
            Assert.Equal(TenantId, tenantId);
            Assert.Equal(UserId, userId);
            Assert.Equal(RbacIds.Member(TenantId, UserId), memberId);
            Assert.Contains(permission, new[] { RbacPermissions.TechnologyInventoryManage,
                RbacPermissions.TechnologyInventoryRestrictedRead });
            RequestedScopes.AddRange(scopes);
            return ValueTask.FromResult(scopes.Any(allowedScopes.Contains));
        }

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }
}
