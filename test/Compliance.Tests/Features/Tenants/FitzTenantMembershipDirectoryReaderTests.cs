using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class FitzTenantMembershipDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();

    [Fact]
    public async Task IsMemberAsyncShouldReturnTrueGivenAStoredMembership()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, TenantId, UserId);
        var reader = new FitzTenantMembershipDirectoryReader(client);

        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId, CancellationToken.None);

        Assert.True(isMember);
    }

    [Fact]
    public async Task IsMemberAsyncShouldReturnFalseGivenNoStoredMembership()
    {
        var reader = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());

        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId, CancellationToken.None);

        Assert.False(isMember);
    }

    [Fact]
    public async Task IsMemberAsyncShouldNotSeeAMembershipStoredUnderADifferentTenant()
    {
        var client = new InMemoryKvClient();
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(client, otherTenantId, UserId);
        var reader = new FitzTenantMembershipDirectoryReader(client);

        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId, CancellationToken.None);

        Assert.False(isMember);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid tenantId, Uuid userId)
    {
        await using var transaction = await client.BeginAsync(
            TenantMembershipDirectoryKeys.Route(tenantId.ToString()), KvDurability.Async, KvMode.ReadWrite);
        await TenantMembershipDirectorySchema.Directory.InsertAsync(
            transaction, new TenantMembershipView(userId, tenantId));
        await transaction.CommitAsync();
    }
}
