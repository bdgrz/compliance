using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class FitzTenantMembershipDirectoryReaderTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();

    [Fact]
    public async Task ListByUserAsyncShouldReturnOnlyTheRequestedUsersMemberships()
    {
        var client = new InMemoryKvClient();
        var otherUserId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        await SeedAsync(client, UserId, tenantId);
        await SeedAsync(client, otherUserId, Uuid.CreateVersion4());
        var reader = new FitzTenantMembershipDirectoryReader(client);

        var page = await reader.ListByUserAsync(UserId, null, null, descending: false, CancellationToken.None);

        var membership = Assert.Single(page.Items);
        Assert.Equal(tenantId, membership.TenantId);
    }

    [Fact]
    public async Task ListByUserAsyncShouldPaginateUsingTheReturnedCursorWithoutAnExtraEmptyPage()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, UserId, Uuid.CreateVersion4());
        await SeedAsync(client, UserId, Uuid.CreateVersion4());
        var reader = new FitzTenantMembershipDirectoryReader(client);

        var firstPage = await reader.ListByUserAsync(UserId, 1, null, descending: false, CancellationToken.None);
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListByUserAsync(
            UserId, 1, firstPage.NextCursor, descending: false, CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid userId, Uuid tenantId)
    {
        await using var transaction = await client.BeginAsync(
            TenantMembershipDirectoryKeys.Route(), KvDurability.Async, KvMode.ReadWrite);
        await TenantMembershipDirectorySchema.Directory.InsertAsync(
            transaction, new TenantMembershipView(userId, tenantId));
        await transaction.CommitAsync();
    }
}
