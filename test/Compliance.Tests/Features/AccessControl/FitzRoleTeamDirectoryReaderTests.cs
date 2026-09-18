using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzRoleTeamDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ListAsyncShouldReturnEveryTeamHoldingTheRole()
    {
        var client = new InMemoryKvClient();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(client, RoleId, first);
        await SeedAsync(client, RoleId, second);
        var reader = new FitzRoleTeamDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ListAsyncShouldOnlyReturnTeamsForTheRequestedRole()
    {
        var client = new InMemoryKvClient();
        var otherRoleId = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var otherTeam = Uuid.CreateVersion4();
        await SeedAsync(client, RoleId, team);
        await SeedAsync(client, otherRoleId, otherTeam);
        var reader = new FitzRoleTeamDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        var result = Assert.Single(page.Items);
        Assert.Equal(team, result.TeamId);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid roleId, Uuid teamId)
    {
        await using var transaction = await client.BeginAsync(
            RoleTeamDirectoryKeys.Route(TenantId.ToString()), KvDurability.Async, KvMode.ReadWrite);
        await RoleTeamDirectorySchema.Directory.InsertAsync(transaction, new RoleTeamView(roleId, teamId));
        await transaction.CommitAsync();
    }
}
