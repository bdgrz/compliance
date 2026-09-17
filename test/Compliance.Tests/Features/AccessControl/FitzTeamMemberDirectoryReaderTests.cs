using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzTeamMemberDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ListAsyncShouldReturnEveryMemberOfTheTeam()
    {
        var client = new InMemoryKvClient();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, first);
        await SeedAsync(client, TeamId, second);
        var reader = new FitzTeamMemberDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ListAsyncShouldOnlyReturnMembersOfTheRequestedTeam()
    {
        var client = new InMemoryKvClient();
        var otherTeamId = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var otherMember = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, member);
        await SeedAsync(client, otherTeamId, otherMember);
        var reader = new FitzTeamMemberDirectoryReader(client);

        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        var result = Assert.Single(page.Items);
        Assert.Equal(member, result.MemberId);
    }

    [Fact]
    public async Task ListAsyncShouldPaginateUsingTheReturnedCursorWithoutAnExtraEmptyPage()
    {
        var client = new InMemoryKvClient();
        await SeedAsync(client, TeamId, Uuid.CreateVersion4());
        await SeedAsync(client, TeamId, Uuid.CreateVersion4());
        var reader = new FitzTeamMemberDirectoryReader(client);

        var firstPage = await reader.ListAsync(
            TenantId, TeamId, 1, null, null, descending: false, CancellationToken.None);
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListAsync(
            TenantId, TeamId, 1, firstPage.NextCursor, null, descending: false, CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
    }

    // Regression test: Search must match a substring anywhere in the member ID, not just a prefix —
    // the KvDirectory 1.3.0 migration briefly narrowed this to prefix-only before being caught and
    // fixed.
    [Fact]
    public async Task ListAsyncShouldFilterByMemberIdSubstringNotJustPrefix()
    {
        var client = new InMemoryKvClient();
        var member = Uuid.CreateVersion4();
        var otherMember = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, member);
        await SeedAsync(client, TeamId, otherMember);
        var reader = new FitzTeamMemberDirectoryReader(client);
        var middleOfMemberId = member.ToString().Substring(9, 8);

        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, middleOfMemberId, descending: false, CancellationToken.None);

        var result = Assert.Single(page.Items);
        Assert.Equal(member, result.MemberId);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid teamId, Uuid memberId)
    {
        await using var transaction = await client.BeginAsync(
            TeamMemberDirectoryKeys.Route(TenantId.ToString()), KvDurability.Async, KvMode.ReadWrite);
        await TeamMemberDirectorySchema.Directory.InsertAsync(transaction, new TeamMemberView(teamId, memberId));
        await transaction.CommitAsync();
    }
}
