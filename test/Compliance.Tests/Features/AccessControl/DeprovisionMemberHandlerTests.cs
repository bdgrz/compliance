using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class DeprovisionMemberHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OtherTenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid ActorUserId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid GrantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly DateTimeOffset ActionAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRemoveAuthorityAndRetainHistoryGivenFreshInvitationAfterDeprovisioning()
    {
        // Arrange
        await using var provider = CreateProvider();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        var memberId = RbacIds.Member(TenantId, UserId);
        await SeedAsync(provider, new Member(TenantId, UserId), member => member.Register());
        var membershipEpisodeId = (await reader.HydrateAsync(new Member(TenantId, UserId)))
            .MembershipEpisodeId;
        await SeedAsync(provider, new TeamMember(TenantId, TeamId, memberId),
            member => member.Assign(membershipEpisodeId));
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, memberId), RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
            new AccessGrantSource("manual", "access-review"),
            ActorReference.ForMember(RbacIds.Member(TenantId, ActorUserId), "Alex Admin"), ActionAt, null);
        await SeedAsync(provider, new AccessGrant(TenantId, GrantId), grant => grant.Issue(terms));
        var teamMembers = new TestTeamMemberDirectory([new TeamMemberView(TeamId, memberId)]);
        var grants = new TestAccessGrantDirectory(new AccessGrantSetView(TenantId, 1,
            [new AccessGrantView(TenantId, GrantId, terms, null, null)]));
        var cleanup = new MemberAuthorityCleanup(reader, executor, teamMembers, grants,
            TimeProvider.System);
        var managerInvariant = new TenantManagerInvariant(reader, executor, teamMembers);
        var handler = new DeprovisionMemberHandler(executor, reader, TimeProvider.System,
            managerInvariant, cleanup);

        // Act
        var deprovision = await handler.HandleAsync(new RequestContext<DeprovisionMember>(
            new DeprovisionMember(TenantId, UserId, "Access is no longer required."), Actor(ActorUserId)),
            CancellationToken.None);
        var reinvited = await new RegisterMemberHandler(executor, reader, cleanup).HandleAsync(
            new RequestContext<RegisterMember>(new RegisterMember(TenantId, UserId), RequestActor.System),
            CancellationToken.None);
        var memberAfterInvite = await reader.HydrateAsync(new Member(TenantId, UserId));
        var assignmentAfterInvite = await reader.HydrateAsync(new TeamMember(TenantId, TeamId, memberId));
        var grantAfterInvite = await reader.HydrateAsync(new AccessGrant(TenantId, GrantId));
        var history = new List<DomainEvent>();
        await foreach (var record in provider.GetRequiredService<IEventStore>()
                           .ReadAsync(new Member(TenantId, UserId).Stream, 0))
            history.Add(record.Event);

        // Assert
        Assert.True(deprovision.IsSuccess);
        Assert.True(reinvited.IsSuccess);
        Assert.True(memberAfterInvite.IsRegistered);
        Assert.False(memberAfterInvite.IsDeprovisioned);
        Assert.False(assignmentAfterInvite.IsAssigned);
        Assert.True(grantAfterInvite.IsRevoked);
        Assert.Contains(history, item => item is MemberDeprovisioned);
        Assert.Contains(history, item => item is MemberDeprovisionCleanupCompleted);
        Assert.Equal(2, history.Count(item => item is MemberRegistered));
    }

    [Fact]
    public async Task ShouldHideMembershipGivenMemberExistsOnlyInAnotherTenant()
    {
        // Arrange
        await using var provider = CreateProvider();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        await SeedAsync(provider, new Member(OtherTenantId, UserId), member => member.Register());
        var teamMembers = new TestTeamMemberDirectory([]);
        var cleanup = new MemberAuthorityCleanup(reader, executor, teamMembers,
            new TestAccessGrantDirectory(new AccessGrantSetView(TenantId, 0, [])),
            TimeProvider.System);
        var handler = new DeprovisionMemberHandler(executor, reader, TimeProvider.System,
            new TenantManagerInvariant(reader, executor, teamMembers), cleanup);

        // Act
        var result = await handler.HandleAsync(new RequestContext<DeprovisionMember>(
            new DeprovisionMember(TenantId, UserId, "Access is no longer required."), Actor(ActorUserId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False((await reader.HydrateAsync(new Member(TenantId, UserId))).IsRegistered);
        Assert.True((await reader.HydrateAsync(new Member(OtherTenantId, UserId))).IsRegistered);
    }

    [Fact]
    public async Task ShouldKeepLastAdministratorGivenDeprovisionRequest()
    {
        // Arrange
        await using var provider = CreateProvider();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        var memberId = RbacIds.Member(TenantId, UserId);
        var administrators = BuiltInRbac.AdministratorsTeamId(TenantId);
        await SeedAsync(provider, new Member(TenantId, UserId), member => member.Register());
        var membershipEpisodeId = (await reader.HydrateAsync(new Member(TenantId, UserId)))
            .MembershipEpisodeId;
        await SeedAsync(provider, new TeamMember(TenantId, administrators, memberId),
            member => member.Assign(membershipEpisodeId));
        var teamMembers = new TestTeamMemberDirectory([new TeamMemberView(administrators, memberId)]);
        var cleanup = new MemberAuthorityCleanup(reader, executor, teamMembers,
            new TestAccessGrantDirectory(new AccessGrantSetView(TenantId, 0, [])), TimeProvider.System);
        var handler = new DeprovisionMemberHandler(executor, reader, TimeProvider.System,
            new TenantManagerInvariant(reader, executor, teamMembers), cleanup);

        // Act
        var result = await handler.HandleAsync(new RequestContext<DeprovisionMember>(
            new DeprovisionMember(TenantId, UserId, "Access is no longer required."), Actor(ActorUserId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.True((await reader.HydrateAsync(new Member(TenantId, UserId))).IsRegistered);
    }

    static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    static async Task SeedAsync<TAggregate>(IServiceProvider provider, TAggregate aggregate,
        Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        var result = await provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            item => AggregateOutcome.Commit(operation(item)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class TestTeamMemberDirectory(IReadOnlyList<TeamMemberView> items) : ITeamMemberDirectoryReader
    {
        public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TeamMemberView>(items.Where(item => item.TeamId == teamId &&
                (search is null || item.MemberId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToArray(), null));

        public ValueTask<IReadOnlyList<TeamMemberView>> ListMemberAssignmentsAsync(Uuid tenantId,
            Uuid memberId, CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<TeamMemberView>>(
            items.Where(item => item.MemberId == memberId).ToArray());
    }

    sealed class TestAccessGrantDirectory(AccessGrantSetView grants) : IAccessGrantDirectory
    {
        public ValueTask<AccessGrantSetView> ListAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(grants);

        public ValueTask<AccessGrantView?> GetAsync(Uuid tenantId, Uuid grantId, CancellationToken ct = default) =>
            ValueTask.FromResult(grants.Grants.SingleOrDefault(item => item.GrantId == grantId));

        public ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId,
            IReadOnlySet<Uuid> grantIds, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlySet<Uuid>>(new HashSet<Uuid>());
    }
}
