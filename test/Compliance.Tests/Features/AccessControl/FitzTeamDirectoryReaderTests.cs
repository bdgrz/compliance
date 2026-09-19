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
    public async Task ShouldReturnStoredTeamGivenMatchingId()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var teamId = Uuid.CreateVersion4();
        await SeedAsync(client, teamId, "Reviewers");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var team = await reader.GetAsync(TenantId, teamId, CancellationToken.None);

        // Assert
        Assert.NotNull(team);
        Assert.Equal("Reviewers", team.Name);
    }

    [Fact]
    public async Task ShouldReturnNullGivenNoSuchTeam()
    {
        // Arrange
        var reader = new FitzTeamDirectoryReader(new InMemoryKvClient());

        // Act
        var team = await reader.GetAsync(TenantId, Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(team);
    }

    [Fact]
    public async Task ShouldReturnTeamsInNameOrderGivenTenantQuery()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(["Alpha", "Beta"], page.Items.Select(team => team.Name));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldPaginateResultsGivenReturnedCursor()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(client, first, "Alpha");
        await SeedAsync(client, second, "Beta");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var firstPage = await reader.ListAsync(TenantId, 1, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListAsync(
            TenantId, 1, firstPage.NextCursor, null, descending: false, CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].TeamId, secondPage.Items[0].TeamId);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task ShouldFilterTeamsGivenNamePrefix()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Reviewers");
        await SeedAsync(client, Uuid.CreateVersion4(), "Administrators");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, null, null, "review", descending: false, CancellationToken.None);

        // Assert
        var team = Assert.Single(page.Items);
        Assert.Equal("Reviewers", team.Name);
    }

    // Regression test: Search must match a substring anywhere in the name, not just a prefix — the
    // KvDirectory 1.3.0 migration briefly narrowed this to prefix-only before being caught and fixed.
    [Fact]
    public async Task ShouldFilterTeamsGivenNameSubstring()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Site Reviewers");
        await SeedAsync(client, Uuid.CreateVersion4(), "Administrators");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, null, null, "review", descending: false, CancellationToken.None);

        // Assert
        var team = Assert.Single(page.Items);
        Assert.Equal("Site Reviewers", team.Name);
    }

    [Fact]
    public async Task ShouldPaginateSearchResultsGivenReturnedCursor()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha Reviewers");
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        await SeedAsync(client, Uuid.CreateVersion4(), "Gamma Reviewers");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var firstPage = await reader.ListAsync(
            TenantId, 1, null, "review", descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(["Alpha Reviewers"], firstPage.Items.Select(team => team.Name));
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListAsync(
            TenantId, 1, firstPage.NextCursor, "review", descending: false, CancellationToken.None);
        Assert.Equal(["Gamma Reviewers"], secondPage.Items.Select(team => team.Name));
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task ShouldSortTeamsDescendingGivenNameOrder()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha");
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(TenantId, null, null, null, descending: true, CancellationToken.None);

        // Assert
        Assert.Equal(["Beta", "Alpha"], page.Items.Select(team => team.Name));
    }

    [Fact]
    public async Task ShouldReturnTeamsOnlyGivenRequestedTenant()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(client, Uuid.CreateVersion4(), "Mine", TenantId);
        await SeedAsync(client, Uuid.CreateVersion4(), "Theirs", otherTenantId);
        var reader = new FitzTeamDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(
            TenantId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var team = Assert.Single(page.Items);
        Assert.Equal("Mine", team.Name);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid teamId, string name, Uuid? tenantId = null)
    {
        var tenant = tenantId ?? TenantId;
        var repository = new FitzTeamDirectoryReader(client);
        var identity = new CheckpointIdentity("TeamDirectory", EventStreamPattern.ForPattern(tenant.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(new TeamDefined(tenant, teamId, name));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
