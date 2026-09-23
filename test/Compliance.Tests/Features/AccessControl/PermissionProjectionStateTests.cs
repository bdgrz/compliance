using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     Regression coverage for a real bug: <see cref="PermissionProjectionState" /> previously keyed
///     <c>RolePermissions</c> by <see cref="Uuid" /> (a <c>Dictionary&lt;Uuid, HashSet&lt;string&gt;&gt;</c>),
///     which System.Text.Json's source-generated <c>UuidJsonConverter</c> cannot serialize as a
///     dictionary key. Every real write to <c>FitzPermissionProjection</c> against a live Fitz
///     broker threw mid-transaction, so no permission grant was ever durably written — every
///     tenant-scoped request was silently forbidden. Fixed by matching the edge-record pattern its
///     siblings (<c>TeamMembers</c>, <c>TeamRoles</c>) already used instead of a dictionary:
///     <c>RolePermissions</c> is now a flat <c>HashSet&lt;RolePermissionEdge&gt;</c>.
///     <see cref="Cntryl.Fitz.Testing.InMemoryKvClient" />-backed tests never exercise real
///     <see cref="JsonSerializer" /> round-trips, so nothing caught this until a manual end-to-end
///     run against a real broker did.
/// </summary>
public sealed class PermissionProjectionStateTests
{
    [Fact]
    public void ShouldRoundTripGivenRealJsonSerialization()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(new TeamDefined(tenantId, teamId, "Reviewers"));
        state.Apply(new TeamMemberAssigned(tenantId, teamId, memberId));
        state.Apply(new RoleDefined(tenantId, roleId, "Reviewer"));
        state.Apply(new RolePermissionAssigned(tenantId, roleId, "tenant:access"));
        state.Apply(new TeamRoleAssigned(tenantId, teamId, roleId));

        var json = JsonSerializer.SerializeToUtf8Bytes(state, ComplianceCoreJsonContext.Default.PermissionProjectionState);

        // Act
        var restored = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.PermissionProjectionState);

        // Assert
        Assert.NotNull(restored);
        var grant = Assert.Single(restored.Materialize());
        Assert.Equal(memberId, grant.MemberId);
        Assert.Equal("tenant:access", grant.Permission);
    }

    [Fact]
    public void ShouldMaterializePermissionGivenActiveMemberAssignedRole()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(new TeamDefined(tenantId, teamId, "Reviewers"));
        state.Apply(new TeamMemberAssigned(tenantId, teamId, memberId));
        state.Apply(new RoleDefined(tenantId, roleId, "Reviewer"));
        state.Apply(new RolePermissionAssigned(tenantId, roleId, "tenant:access"));

        // Act
        state.Apply(new TeamRoleAssigned(tenantId, teamId, roleId));

        // Assert
        var grant = Assert.Single(state.Materialize());

        Assert.Equal(memberId, grant.MemberId);
        Assert.Equal("tenant:access", grant.Permission);
    }

    [Fact]
    public void ShouldNotMaterializePermissionGivenFirmStaffTeamAssignment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(tenantId, memberId, Uuid.CreateVersion4(), "firm_staff"));
        state.Apply(new TeamDefined(tenantId, teamId, "Administrators"));
        state.Apply(new RoleDefined(tenantId, roleId, "Tenant Administration"));
        state.Apply(new TeamRoleAssigned(tenantId, teamId, roleId));
        state.Apply(new RolePermissionAssigned(tenantId, roleId, RbacPermissions.TenantAccess));

        // Act
        state.Apply(new TeamMemberAssigned(tenantId, teamId, memberId));

        // Assert
        Assert.Empty(state.Materialize());
        Assert.Empty(state.Explain(memberId));
    }

    [Fact]
    public void ShouldRemovePermissionGivenTeamMemberRemoval()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(new TeamDefined(tenantId, teamId, "Reviewers"));
        state.Apply(new TeamMemberAssigned(tenantId, teamId, memberId));
        state.Apply(new RoleDefined(tenantId, roleId, "Reviewer"));
        state.Apply(new RolePermissionAssigned(tenantId, roleId, "tenant:access"));
        state.Apply(new TeamRoleAssigned(tenantId, teamId, roleId));

        // Act
        state.Apply(new TeamMemberRemoved(tenantId, teamId, memberId));

        // Assert
        Assert.Empty(state.Materialize());
    }

    [Fact]
    public void ShouldExplainCurrentRolePathGivenPermissionChanges()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(tenantId, memberId, Uuid.CreateVersion4()));
        state.Apply(new TeamDefined(tenantId, teamId, "Reviewers"));
        state.Apply(new RoleDefined(tenantId, roleId, "Reviewer"));
        state.Apply(new TeamMemberAssigned(tenantId, teamId, memberId));
        state.Apply(new TeamRoleAssigned(tenantId, teamId, roleId));
        state.Apply(new RolePermissionAssigned(tenantId, roleId, "tenant.access"));
        state.Apply(new RolePermissionAssigned(tenantId, roleId, "program.manage"));

        // Act
        var before = Assert.Single(state.Explain(memberId));
        state.Apply(new RolePermissionRemoved(tenantId, roleId, "program.manage"));
        var after = Assert.Single(state.Explain(memberId));
        state.Apply(new TeamMemberRemoved(tenantId, teamId, memberId));

        // Assert
        Assert.Equal(teamId, before.TeamId);
        Assert.Equal(roleId, before.RoleId);
        Assert.Equal(["program.manage", "tenant.access"], before.Permissions);
        Assert.Equal(["tenant.access"], after.Permissions);
        Assert.Empty(state.Explain(memberId));
        Assert.Empty(state.Explain(Uuid.CreateVersion4()));
    }
}
