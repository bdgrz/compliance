using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzRolePermissionDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryPermissionGivenRoleQuery()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, RoleId, "controls.read");
        await SeedAsync(client, RoleId, "controls.manage");
        var reader = new FitzRolePermissionDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldReturnPermissionsOnlyGivenRequestedRole()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var otherRoleId = Uuid.CreateVersion4();
        await SeedAsync(client, RoleId, "controls.read");
        await SeedAsync(client, otherRoleId, "controls.manage");
        var reader = new FitzRolePermissionDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal("controls.read", result.Permission);
    }

    [Fact]
    public async Task ShouldFilterPermissionsGivenSubstring()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, RoleId, "controls.read");
        await SeedAsync(client, RoleId, "tenant.access");
        var reader = new FitzRolePermissionDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, "trols", descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal("controls.read", result.Permission);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid roleId, string permission)
    {
        var repository = new FitzRolePermissionDirectoryReader(client);
        var identity = new CheckpointIdentity("RolePermissionDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(new RolePermissionAssigned(TenantId, roleId, permission));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
