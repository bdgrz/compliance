using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class TechnologyInventoryAuthorizerTests
{
    [Fact]
    public async Task ShouldRequireTechnologyInventoryPermissionGivenActiveTenantMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new TechnologyInventoryAuthorizer(
            new FixedMembershipDirectory(true), new ActiveTenant(), permissions);
        var context = new RequestContext<ITechnologyInventoryRequest>(
            new ListTechnologyComponents(tenantId), BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("technology_inventory.manage", Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldAllowOnlyAssetAndFlowReadsGivenScopedInventoryCapability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var resourceId = Uuid.CreateVersion4();
        var scoped = new ScopedInventoryPermission();
        var authorizer = new TechnologyInventoryAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), new RecordingPermissionAuthorizer(false), scoped);
        ITechnologyInventoryRequest[] requests =
        [
            new GetInformationAsset(tenantId, resourceId),
            new ListInformationAssets(tenantId),
            new ListInformationAssetRevisions(tenantId, resourceId),
            new ListInformationAssetBoundaryReferences(tenantId, resourceId),
            new PreviewInformationAssetChange(tenantId, resourceId, 1),
            new GetDataFlow(tenantId, resourceId),
            new ListDataFlows(tenantId),
            new ListDataFlowRevisions(tenantId, resourceId),
        ];

        // Act
        var results = new List<Result>();
        foreach (var request in requests)
            results.Add(await authorizer.AuthorizeAsync(new RequestContext<ITechnologyInventoryRequest>(
                request, BdgrzActor(userId)), CancellationToken.None));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(requests.Length, scoped.CallCount);
    }

    sealed class ScopedInventoryPermission : IAccessGrantScopePermissionAuthorizer
    {
        public int CallCount { get; private set; }

        public ValueTask<bool> IsAllowedAtAnyTechnologyInventoryScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default)
        {
            CallCount++;
            Assert.Equal(RbacPermissions.TechnologyInventoryManage, permission);
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
