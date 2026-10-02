using System.Security.Claims;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ListTeamMembersHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldShowAuthenticatedNameGivenCanonicalTenantMemberSource()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(TenantId, userId);
        await fixture.Repository.ExecuteAsync(new Member(TenantId, userId),
            member => AggregateOutcome.CommitOnSuccess(member.Register()),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var reader = new FakeTeamMemberDirectoryReader();
        reader.Members[(TenantId, TeamId)] = [new TeamMemberView(TeamId, memberId)];
        var handler = new ListTeamMembersHandler(reader, fixture.Store,
            new FixedMembershipDirectory(true), profiles: new Names(userId));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTeamMembers>(
            new ListTeamMembers(TenantId, TeamId), Actor()), CancellationToken.None);

        // Assert
        var member = Assert.Single(result.Value.Items);
        Assert.Equal(userId, member.UserId);
        Assert.Equal("Provider member", member.DisplayName);
        Assert.Null(member.VerifiedEmailAddress);
    }

    [Fact]
    public async Task ShouldFailTransientlyGivenCanonicalMemberHasNotProjected()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(TenantId, userId);
        await fixture.Repository.ExecuteAsync(new Member(TenantId, userId),
            member => AggregateOutcome.CommitOnSuccess(member.Register()),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var reader = new FakeTeamMemberDirectoryReader();
        reader.Members[(TenantId, TeamId)] = [new TeamMemberView(TeamId, memberId)];
        var handler = new ListTeamMembersHandler(reader, fixture.Store, new FixedMembershipDirectory(false));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTeamMembers>(
            new ListTeamMembers(TenantId, TeamId), Actor()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldKeepUserUnknownGivenMemberIdFromAnotherTenant()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var otherTenant = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(otherTenant, userId);
        await fixture.Repository.ExecuteAsync(new Member(otherTenant, userId),
            member => AggregateOutcome.CommitOnSuccess(member.Register()),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var reader = new FakeTeamMemberDirectoryReader();
        reader.Members[(TenantId, TeamId)] = [new TeamMemberView(TeamId, memberId)];
        var handler = new ListTeamMembersHandler(reader, fixture.Store,
            new FixedMembershipDirectory(true), profiles: new Names(userId));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTeamMembers>(
            new ListTeamMembers(TenantId, TeamId), Actor()), CancellationToken.None);

        // Assert
        var member = Assert.Single(result.Value.Items);
        Assert.Null(member.UserId);
        Assert.Equal(memberId.ToString(), member.DisplayName);
        Assert.Null(member.VerifiedEmailAddress);
    }

    sealed class Names(Uuid userId) : IUserDisplayNameReader
    {
        public ValueTask<string?> ReadAsync(Uuid target, CancellationToken ct = default) =>
            ValueTask.FromResult(target == userId ? "Provider member" : null);
    }

    [Fact]
    public async Task ShouldReturnPageGivenReaderResult()
    {
        // Arrange
        var reader = new FakeTeamMemberDirectoryReader();
        reader.Members[(TenantId, TeamId)] = [new TeamMemberView(TeamId, MemberId)];
        var handler = new ListTeamMembersHandler(reader);
        var context = new RequestContext<ListTeamMembers>(new ListTeamMembers(TenantId, TeamId), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var member = Assert.Single(result.Value.Items);
        Assert.Equal(MemberId, member.MemberId);
    }

    [Fact]
    public async Task ShouldBuildNormalizedQueryGivenRequest()
    {
        // Arrange
        var reader = new FakeTeamMemberDirectoryReader();
        var handler = new ListTeamMembersHandler(reader);
        var context = new RequestContext<ListTeamMembers>(
            new ListTeamMembers(TenantId, TeamId, Limit: 5, Cursor: "opaque", Search: "  abc  ", Sort: "member_id:desc"),
            Actor());

        // Act
        _ = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
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
