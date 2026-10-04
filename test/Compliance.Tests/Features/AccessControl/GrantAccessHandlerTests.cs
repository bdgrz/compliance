using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class GrantAccessHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid PrincipalMemberId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly Uuid GrantId = Uuid.CreateVersion4();
    static readonly DateTimeOffset EffectiveFrom = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldBindMemberGrantToCurrentMembershipEpisodeGivenValidProposal()
    {
        // Arrange
        await using var provider = CreateProvider();
        var episodeId = Uuid.CreateVersion4();
        var handler = new GrantAccessHandler(provider.GetRequiredService<IAggregateExecutor>(),
            new ValidProposalValidator(), new TestMemberAccessEligibility(episodeId, episodeId));
        var request = Request();

        // Act
        var result = await handler.HandleAsync(new RequestContext<GrantAccess>(request, Actor()),
            CancellationToken.None);
        var history = new List<DomainEvent>();
        await foreach (var record in provider.GetRequiredService<IEventStore>()
                           .ReadAsync(new AccessGrant(TenantId, GrantId).Stream, 0))
            history.Add(record.Event);

        // Assert
        Assert.True(result.IsSuccess);
        var issued = Assert.IsType<AccessGrantIssued>(Assert.Single(history));
        Assert.Equal(episodeId, issued.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldRejectMemberGrantGivenMembershipEpisodeChangesDuringValidation()
    {
        // Arrange
        await using var provider = CreateProvider();
        var firstEpisodeId = Uuid.CreateVersion4();
        var secondEpisodeId = Uuid.CreateVersion4();
        var handler = new GrantAccessHandler(provider.GetRequiredService<IAggregateExecutor>(),
            new ValidProposalValidator(), new TestMemberAccessEligibility(firstEpisodeId, secondEpisodeId));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GrantAccess>(Request(), Actor()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        await foreach (var record in provider.GetRequiredService<IEventStore>()
                           .ReadAsync(new AccessGrant(TenantId, GrantId).Stream, 0))
            Assert.IsNotType<AccessGrantIssued>(record.Event);
    }

    static GrantAccess Request() => new(TenantId, GrantId, new AccessGrantProposal(
        new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, PrincipalMemberId), RoleId,
        new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
        new AccessGrantSource("manual", "access-review"), EffectiveFrom, null));

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    sealed class ValidProposalValidator : IAccessGrantProposalValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);
    }

    sealed class TestMemberAccessEligibility(params Uuid?[] episodes) : IMemberAccessEligibility
    {
        int _episodeIndex;

        public ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(true);

        public ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid memberId,
            CancellationToken ct = default)
        {
            var index = Math.Min(_episodeIndex++, episodes.Length - 1);
            return ValueTask.FromResult(episodes[index]);
        }
    }
}
