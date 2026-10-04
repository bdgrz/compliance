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

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
