using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ChangeTenantSlugHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAttributeMemberGivenSlugChangeAndConfirmation()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("acme-next"),
            CancellationToken.None);
        var requested = Assert.Single((await scenario.TenantEventsAsync())
            .Select(static record => record.Event).OfType<TenantSlugChangeRequested>());
        var replayed = await scenario.Reader.HydrateAsync(new Tenant(scenario.TenantId),
            CancellationToken.None);
        var confirmation = replayed.ConfirmSlug("acme-next");
        var changed = Assert.Single(new AggregateScenario<Tenant>(replayed).PendingEvents
            .OfType<TenantSlugChanged>());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(confirmation.IsSuccess);
        Assert.Equal(ActorReference.ForMember(RbacIds.Member(scenario.TenantId,
            scenario.UserId), "org-admin@example.test"), requested.RequestedBy);
        Assert.Equal(Now, requested.RequestedAt);
        Assert.Equal(requested.RequestedBy, changed.RequestedBy);
        Assert.Equal(requested.RequestedAt, changed.RequestedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectSlugGivenAnotherTenantOwnsOrRetiredIt(bool retire)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        await scenario.SeedSlugAsync("claimed", Uuid.CreateVersion4(), retire);

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("claimed"),
            CancellationToken.None);
        var requests = (await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>().ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task ShouldRejectReservedSlugGivenOrgAdminRequest()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("api"),
            CancellationToken.None);
        var requests = (await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>().ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(requests);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ac--me")]
    public async Task ShouldRejectMalformedSlugGivenOrgAdminRequest(string slug)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context(slug),
            CancellationToken.None);
        var requests = (await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>().ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(requests);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _events;

        Scenario(ServiceProvider provider, AsyncServiceScope scope, InMemoryEventStore events,
            Uuid tenantId, Uuid userId)
        {
            _provider = provider;
            _scope = scope;
            _events = events;
            TenantId = tenantId;
            UserId = userId;
        }

        public Uuid TenantId { get; }
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public ChangeTenantSlugHandler Handler => new(Reader, Executor,
            new FixedOperatorAccess(), new FixedTimeProvider(Now));

        public static async Task<Scenario> CreateAsync()
        {
            var events = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(events);
            services.AddPortia();
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var scenario = new Scenario(provider, scope, events, Uuid.CreateVersion4(),
                Uuid.CreateVersion4());
            var tenant = new Tenant(scenario.TenantId);
            Assert.True(tenant.Register(Uuid.CreateVersion4(), "Acme", "acme").IsSuccess);
            Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
            await scenario.Writer.SaveAsync(tenant,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            return scenario;
        }

        public FixedRequestContext Context(string slug) =>
            new(new ChangeTenantSlug(TenantId, slug), Actor(UserId), Uuid.CreateVersion4());

        public async Task<List<DomainEventRecord>> TenantEventsAsync()
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _events.ReadAsync(new EventStreamAddress("bdgrz",
                               "tenants", TenantId.ToString()), 0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async Task SeedSlugAsync(string slug, Uuid ownerId, bool retired)
        {
            var candidate = await Reader.HydrateAsync(new TenantSlug(slug), CancellationToken.None);
            Assert.True(candidate.Register(ownerId).IsSuccess);
            if (retired)
                Assert.True(candidate.Surrender(ownerId).IsSuccess);
            await Writer.SaveAsync(candidate, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedRequestContext(ChangeTenantSlug request, ClaimsPrincipal actor, Uuid requestId)
        : IRequestContext<ChangeTenantSlug>
    {
        public ChangeTenantSlug Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = Now;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "org-admin@example.test")], "BdgrzSession"));

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
