using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     Exercises the real KvDirectory-backed index/cursor/search logic against Fitz's own in-memory
///     KV double, closing the gap where these adapters had zero direct coverage.
/// </summary>
public sealed class FitzTeamDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task GetAsyncShouldReturnTheStoredTeam()
    {
        var client = new InMemoryKvClient();
        var teamId = Uuid.CreateVersion4();
        await SeedAsync(client, teamId, "Reviewers");
        var reader = new FitzTeamDirectoryReader(client);

        var team = await reader.GetAsync(TenantId, teamId, CancellationToken.None);

        Assert.NotNull(team);
        Assert.Equal("Reviewers", team.Name);
    }

    [Fact]
    public async Task GetAsyncShouldReturnNullGivenNoSuchTeam()
    {
        var reader = new FitzTeamDirectoryReader(new InMemoryKvClient());

        var team = await reader.GetAsync(TenantId, Uuid.CreateVersion4(), CancellationToken.None);

        Assert.Null(team);
    }

    [Fact]
    public async Task ListAsyncShouldReturnEveryTeamInNameOrder()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha");
        var reader = new FitzTeamDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, null, null, null, descending: false, CancellationToken.None);

        Assert.Equal(["Alpha", "Beta"], page.Items.Select(team => team.Name));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ListAsyncShouldPaginateUsingTheReturnedCursor()
    {
        var client = new InMemoryKvClient();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(client, first, "Alpha");
        await SeedAsync(client, second, "Beta");
        var reader = new FitzTeamDirectoryReader(client);

        var firstPage = await reader.ListAsync(TenantId, 1, null, null, descending: false, CancellationToken.None);
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListAsync(
            TenantId, 1, firstPage.NextCursor, null, descending: false, CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].TeamId, secondPage.Items[0].TeamId);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task ListAsyncShouldFilterByNamePrefix()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Reviewers");
        await SeedAsync(client, Uuid.CreateVersion4(), "Administrators");
        var reader = new FitzTeamDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, null, null, "review", descending: false, CancellationToken.None);

        var team = Assert.Single(page.Items);
        Assert.Equal("Reviewers", team.Name);
    }

    [Fact]
    public async Task ListAsyncShouldSortByNameDescending()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha");
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        var reader = new FitzTeamDirectoryReader(client);

        var page = await reader.ListAsync(TenantId, null, null, null, descending: true, CancellationToken.None);

        Assert.Equal(["Beta", "Alpha"], page.Items.Select(team => team.Name));
    }

    [Fact]
    public async Task ListAsyncShouldOnlyReturnTeamsForTheRequestedTenant()
    {
        var client = new InMemoryKvClient();
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(client, Uuid.CreateVersion4(), "Mine", TenantId);
        await SeedAsync(client, Uuid.CreateVersion4(), "Theirs", otherTenantId);
        var reader = new FitzTeamDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, null, null, null, descending: false, CancellationToken.None);

        var team = Assert.Single(page.Items);
        Assert.Equal("Mine", team.Name);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid teamId, string name, Uuid? tenantId = null)
    {
        await using var transaction = await client.BeginAsync(
            TeamDirectoryKeys.Route((tenantId ?? TenantId).ToString()), KvDurability.Async, KvMode.ReadWrite);
        await TeamDirectorySchema.Directory.InsertAsync(transaction, new TeamView(teamId, name));
        await transaction.CommitAsync();
    }
}
