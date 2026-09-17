using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ListTeamMembersHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnThePageFromTheReader()
    {
        var reader = new FakeTeamMemberDirectoryReader();
        reader.Members[(TenantId, TeamId)] = [new TeamMemberView(TeamId, MemberId)];
        var handler = new ListTeamMembersHandler(reader);
        var context = new RequestContext<ListTeamMembers>(new ListTeamMembers(TenantId, TeamId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var member = Assert.Single(result.Value.Items);
        Assert.Equal(MemberId, member.MemberId);
    }

    [Fact]
    public async Task ShouldBuildANormalizedQueryFromTheRequest()
    {
        var reader = new FakeTeamMemberDirectoryReader();
        var handler = new ListTeamMembersHandler(reader);
        var context = new RequestContext<ListTeamMembers>(
            new ListTeamMembers(TenantId, TeamId, Limit: 5, Cursor: "opaque", Search: "  abc  ", Sort: "member_id:desc"),
            Actor());

        _ = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(5, reader.LastLimit);
        Assert.Equal("opaque", reader.LastCursor);
        Assert.Equal("abc", reader.LastSearch);
        Assert.True(reader.LastDescending);
        Assert.Equal(TeamId, reader.LastTeamId);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakeTeamMemberDirectoryReader : ITeamMemberDirectoryReader
    {
        public Dictionary<(Uuid TenantId, Uuid TeamId), List<TeamMemberView>> Members { get; } = [];
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastTeamId { get; private set; }

        public ValueTask<Page<TeamMemberView>> ListAsync(
            Uuid tenantId,
            Uuid teamId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default)
        {
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            LastTeamId = teamId;
            IReadOnlyList<TeamMemberView> items = Members.TryGetValue((tenantId, teamId), out var members)
                ? [.. members.Take(limit ?? 50)]
                : [];
            return ValueTask.FromResult(new Page<TeamMemberView>(items, null));
        }
    }
}
