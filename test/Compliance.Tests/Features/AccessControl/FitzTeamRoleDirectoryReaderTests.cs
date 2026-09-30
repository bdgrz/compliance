using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzTeamRoleDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryRoleGivenTeamQuery()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await ApplyAsync(client, new TeamRoleAssigned(TenantId, TeamId, Uuid.CreateVersion4()));
        await ApplyAsync(client, new TeamRoleAssigned(TenantId, TeamId, Uuid.CreateVersion4()));
        var reader = new FitzTeamRoleDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(TeamId, item.TeamId));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldReturnRolesOnlyGivenRequestedTeam()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var role = Uuid.CreateVersion4();
        await ApplyAsync(client, new TeamRoleAssigned(TenantId, TeamId, role));
        await ApplyAsync(client, new TeamRoleAssigned(TenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()));
        var reader = new FitzTeamRoleDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(role, result.RoleId);
    }

    [Fact]
    public async Task ShouldDropRoleGivenTeamRoleRemoved()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var kept = Uuid.CreateVersion4();
        var removed = Uuid.CreateVersion4();
        await ApplyAsync(client, new TeamRoleAssigned(TenantId, TeamId, kept));
        await ApplyAsync(client, new TeamRoleAssigned(TenantId, TeamId, removed));
        await ApplyAsync(client, new TeamRoleRemoved(TenantId, TeamId, removed));
        var reader = new FitzTeamRoleDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(kept, Assert.Single(page.Items).RoleId);
    }

    static async Task ApplyAsync(InMemoryKvClient client, DomainEvent domainEvent)
    {
        var repository = new FitzTeamRoleDirectoryReader(client);
        var identity = new CheckpointIdentity("TeamRoleDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
