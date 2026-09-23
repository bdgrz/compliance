using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzPermissionAuthorizerTests
{
    [Fact]
    public async Task ShouldDenyFirmStaffGivenPersistedPreUpgradePermissionGrant()
    {
        // Arrange: an old permission projection has a grant, while membership says firm_staff.
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var memberships = new FitzTenantMembershipDirectoryReader(client);
        var permissions = new FitzPermissionAuthorizer(client, memberships);
        var permissionIdentity = new CheckpointIdentity("PermissionProjection",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await permissions.BeginAsync(new ProjectionBatchContext(
                         permissionIdentity, ProjectionCheckpoint.Start)))
        {
            await permissions.ApplyAsync(new MemberRegistered(tenantId, memberId, userId));
            await permissions.ApplyAsync(new TeamDefined(tenantId, teamId, "Administrators"));
            await permissions.ApplyAsync(new RoleDefined(tenantId, roleId, "Tenant Administration"));
            await permissions.ApplyAsync(new TeamMemberAssigned(tenantId, teamId, memberId));
            await permissions.ApplyAsync(new TeamRoleAssigned(tenantId, teamId, roleId));
            await permissions.ApplyAsync(new RolePermissionAssigned(tenantId, roleId,
                RbacPermissions.TenantAccess));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var membershipIdentity = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await memberships.BeginAsync(new ProjectionBatchContext(
                         membershipIdentity, ProjectionCheckpoint.Start)))
        {
            await memberships.ApplyAsync(new MemberRegistered(tenantId, memberId, userId,
                "firm_staff"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
            RbacPermissions.TenantAccess);

        // Assert
        Assert.False(allowed);
    }
}
