using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class FitzTenantDirectoryReaderTests
{
    [Fact]
    public async Task GetAsyncShouldReturnTheStoredTenant()
    {
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        await SeedAsync(client, tenantId, "Acme", "acme");
        var reader = new FitzTenantDirectoryReader(client);

        var tenant = await reader.GetAsync(tenantId, CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal("Acme", tenant.Name);
        Assert.Equal("acme", tenant.Slug);
    }

    [Fact]
    public async Task GetAsyncShouldReturnNullGivenNoSuchTenant()
    {
        var reader = new FitzTenantDirectoryReader(new InMemoryKvClient());

        var tenant = await reader.GetAsync(Uuid.CreateVersion4(), CancellationToken.None);

        Assert.Null(tenant);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid tenantId, string name, string slug)
    {
        await using var transaction = await client.BeginAsync(
            TenantDirectoryKeys.Route(), KvDurability.Async, KvMode.ReadWrite);
        await TenantDirectorySchema.Directory.InsertAsync(transaction, new TenantView(tenantId, name, slug));
        await transaction.CommitAsync();
    }
}
