using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class InternalRbacTests
{
    static readonly Uuid TenantId = Id("11f455d2-fb10-4f28-a157-23e18e706e70");
    static readonly Uuid UserId = Id("0862062f-97e9-45de-a312-0f884c48180d");
    static readonly Uuid TeamId = Id("2cc8e854-a49a-42a1-9581-d0622de3d5c3");
    static readonly Uuid RoleId = Id("c1b1b99d-2c52-46c6-ad58-55fe90bf53b3");

    [Fact]
    public void ShouldGrantAdditivePermissionsOnlyThroughTeamRolePath()
    {
        var member = new Member(TenantId, UserId);
        var team = new Team(TenantId, TeamId);
        var teamMember = new TeamMember(TenantId, TeamId, member.Id);
        var role = new Role(TenantId, RoleId);
        var read = new RolePermission(TenantId, RoleId, "controls.read");
        var manage = new RolePermission(TenantId, RoleId, "controls.manage");
        var teamRole = new TeamRole(TenantId, TeamId, RoleId);
        _ = member.Register();
        _ = team.Define("Compliance administrators");
        _ = teamMember.Assign();
        _ = role.Define("Compliance administrator");
        _ = read.Assign();
        _ = manage.Assign();
        _ = teamRole.Assign();

        var permissions = Rbac(member, team, teamMember, role, read, manage, teamRole)
            .GetPermissions(member.Id);

        Assert.Equal(["controls.manage", "controls.read"], permissions);
    }

    [Fact]
    public void ShouldNotGrantRoleDirectlyToMember()
    {
        Assert.DoesNotContain(
            typeof(Role).Assembly.GetTypes(),
            type => type.Namespace == typeof(Role).Namespace && type.Name == "MemberRole");
    }

    [Fact]
    public void ShouldNormalizePermissionAndRelationshipIdentities()
    {
        var first = new RolePermission(TenantId, RoleId, " Controls.Read ");
        var repeated = new RolePermission(TenantId, RoleId, "controls.read");
        var member = new Member(TenantId, UserId);

        Assert.Equal(first.Id, repeated.Id);
        Assert.Equal(member.Id, new Member(TenantId, UserId).Id);
        Assert.True(first.Assign().IsSuccess);
        var assigned = Assert.Single(new AggregateScenario<RolePermission>(first).PendingEvents);
        Assert.Equal("controls.read", assigned.GetType().GetProperty("Permission")?.GetValue(assigned));
    }

    [Fact]
    public void ShouldProtectBuiltInTeamsAndRolesFromDeletion()
    {
        var team = new Team(TenantId, BuiltInRbac.AdministratorsTeamId(TenantId));
        var role = new Role(TenantId, BuiltInRbac.TenantAdministrationRoleId(TenantId));
        _ = team.Define(BuiltInRbac.AdministratorsTeamName);
        _ = role.Define(BuiltInRbac.TenantAdministrationRoleName);

        var teamDeletion = team.Delete();
        var roleDeletion = role.Delete();

        Assert.False(teamDeletion.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, teamDeletion.Error.Kind);
        Assert.False(roleDeletion.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, roleDeletion.Error.Kind);
    }

    [Fact]
    public void ShouldMaterializeAndRevokePermissionWithoutWalkingGraphAtCheckTime()
    {
        var memberId = RbacIds.Member(TenantId, UserId);
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(TenantId, memberId, UserId));
        state.Apply(new TeamDefined(TenantId, TeamId, "Reviewers"));
        state.Apply(new RoleDefined(TenantId, RoleId, "Control reviewer"));
        state.Apply(new TeamMemberAssigned(TenantId, TeamId, memberId));
        state.Apply(new TeamRoleAssigned(TenantId, TeamId, RoleId));
        state.Apply(new RolePermissionAssigned(TenantId, RoleId, "controls.read"));

        Assert.Contains(new PermissionGrant(memberId, "controls.read"), state.Materialize());

        state.Apply(new RoleDeleted(TenantId, RoleId));

        Assert.Empty(state.Materialize());
    }

    [Fact]
    public void ShouldRevokePermissionWhenAMemberLeavesATeam()
    {
        var memberId = RbacIds.Member(TenantId, UserId);
        var state = new PermissionProjectionState();
        state.Apply(new MemberRegistered(TenantId, memberId, UserId));
        state.Apply(new TeamDefined(TenantId, TeamId, "Reviewers"));
        state.Apply(new RoleDefined(TenantId, RoleId, "Control reviewer"));
        state.Apply(new TeamMemberAssigned(TenantId, TeamId, memberId));
        state.Apply(new TeamRoleAssigned(TenantId, TeamId, RoleId));
        state.Apply(new RolePermissionAssigned(TenantId, RoleId, "controls.read"));
        Assert.Contains(new PermissionGrant(memberId, "controls.read"), state.Materialize());

        state.Apply(new TeamMemberRemoved(TenantId, TeamId, memberId));

        Assert.Empty(state.Materialize());
    }

    [Fact]
    public void ShouldUseTheSamePermissionKeyForEquivalentPermissionStrings()
    {
        Assert.Equal(
            PermissionProjectionKeys.Grant(RbacIds.Member(TenantId, UserId), " Controls.Read ").ToArray(),
            PermissionProjectionKeys.Grant(RbacIds.Member(TenantId, UserId), "controls.read").ToArray());
    }

    static RbacGraph Rbac(params Aggregate[] aggregates)
    {
        var graph = new RbacGraph(TenantId);
        foreach (var aggregate in aggregates)
            foreach (var domainEvent in new AggregateScenario<Aggregate>(aggregate).PendingEvents)
                graph.Apply(domainEvent);
        return graph;
    }

    static Uuid Id(string value) => Uuid.Parse(value, CultureInfo.InvariantCulture);
}
