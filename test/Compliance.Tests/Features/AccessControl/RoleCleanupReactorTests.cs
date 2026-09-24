using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveRelationshipsGivenDeletedRole()
    {
        // Arrange
        var firstTeam = Uuid.CreateVersion4();
        var secondTeam = Uuid.CreateVersion4();
        var permissions = new FakeRolePermissionDirectoryReader(
            new RolePermissionView(RoleId, "controls.read"), new RolePermissionView(RoleId, "controls.manage"));
        var teams = new FakeRoleTeamDirectoryReader(
            new RoleTeamView(RoleId, firstTeam), new RoleTeamView(RoleId, secondTeam));
        var scenario = new ReactorScenario().Given(new RoleDeleted(TenantId, RoleId));

        // Act
        await scenario.RunAsync(new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, permissions, teams));

        // Assert
        Assert.Equal(4, scenario.SentRequests.Count);
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveRolePermission removal && removal.Permission == "controls.read");
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveRolePermission removal && removal.Permission == "controls.manage");
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveTeamRole removal && removal.TeamId == firstTeam);
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveTeamRole removal && removal.TeamId == secondTeam);
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyRole()
    {
        // Arrange
        var permissions = new FakeRolePermissionDirectoryReader();
        var teams = new FakeRoleTeamDirectoryReader();
        var scenario = new ReactorScenario().Given(new RoleDeleted(TenantId, RoleId));

        // Act
        await scenario.RunAsync(new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, permissions, teams));

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    sealed class FakeRolePermissionDirectoryReader(params RolePermissionView[] items) : IRolePermissionDirectoryReader
    {
        public ValueTask<Page<RolePermissionView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RolePermissionView>(items, null));
    }

    sealed class FakeRoleTeamDirectoryReader(params RoleTeamView[] items) : IRoleTeamDirectoryReader
    {
        public ValueTask<Page<RoleTeamView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RoleTeamView>(items, null));
    }
}
