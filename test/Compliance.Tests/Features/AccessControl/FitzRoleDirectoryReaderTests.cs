using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     Exercises the real KvDirectory-backed index/cursor/search logic against Fitz's own in-memory
///     KV double — same coverage as <see cref="FitzTeamDirectoryReaderTests" />.
/// </summary>
public sealed class FitzRoleDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task GetAsyncShouldReturnTheStoredRole()
    {
        var client = new InMemoryKvClient();
        var roleId = Uuid.CreateVersion4();
        await SeedAsync(client, roleId, "Reviewer");
        var reader = new FitzRoleDirectoryReader(client);

        var role = await reader.GetAsync(TenantId, roleId, CancellationToken.None);

        Assert.NotNull(role);
        Assert.Equal("Reviewer", role.Name);
    }

    [Fact]
    public async Task GetAsyncShouldReturnNullGivenNoSuchRole()
    {
        var reader = new FitzRoleDirectoryReader(new InMemoryKvClient());

        var role = await reader.GetAsync(TenantId, Uuid.CreateVersion4(), CancellationToken.None);

        Assert.Null(role);
    }

    [Fact]
    public async Task ListAsyncShouldReturnEveryRoleInNameOrder()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha");
        var reader = new FitzRoleDirectoryReader(client);

        var page = await reader.ListAsync(TenantId, null, null, null, descending: false, CancellationToken.None);

        Assert.Equal(["Alpha", "Beta"], page.Items.Select(role => role.Name));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ListAsyncShouldFilterByNameSubstring()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Site Reviewer");
        await SeedAsync(client, Uuid.CreateVersion4(), "Administrator");
        var reader = new FitzRoleDirectoryReader(client);

        var page = await reader.ListAsync(TenantId, null, null, "review", descending: false, CancellationToken.None);

        var role = Assert.Single(page.Items);
        Assert.Equal("Site Reviewer", role.Name);
    }

    [Fact]
    public async Task ListAsyncShouldOnlyReturnRolesForTheRequestedTenant()
    {
        var client = new InMemoryKvClient();
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(client, Uuid.CreateVersion4(), "Mine", TenantId);
        await SeedAsync(client, Uuid.CreateVersion4(), "Theirs", otherTenantId);
        var reader = new FitzRoleDirectoryReader(client);

        var page = await reader.ListAsync(TenantId, null, null, null, descending: false, CancellationToken.None);

        var role = Assert.Single(page.Items);
        Assert.Equal("Mine", role.Name);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid roleId, string name, Uuid? tenantId = null)
    {
        await using var transaction = await client.BeginAsync(
            RoleDirectoryKeys.Route((tenantId ?? TenantId).ToString()), KvDurability.Async, KvMode.ReadWrite);
        await RoleDirectorySchema.Directory.InsertAsync(transaction, new RoleView(roleId, name));
        await transaction.CommitAsync();
    }
}
