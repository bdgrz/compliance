using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class SetCriteriaTextOverlayHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    static readonly CriteriaTextOverlayContent Content = new("Synthetic supplied text",
        "Test supplier", "Test license", new CriteriaOverlayUsageFlags(true, true));

    [Fact]
    public async Task ShouldReturnOriginalRevisionGivenReplayAfterLaterRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var firstRequestId = Uuid.CreateVersion4();
        var firstRequest = scenario.Request(firstRequestId, expectedRevision: null, Content);
        var first = await scenario.Handler.HandleAsync(firstRequest, CancellationToken.None);
        Assert.True(first.IsSuccess);
        var second = await scenario.Handler.HandleAsync(scenario.Request(Uuid.CreateVersion4(),
            expectedRevision: 1, Content with { Text = "Revised synthetic text" }),
            CancellationToken.None);
        Assert.True(second.IsSuccess);

        // Act
        var retry = await scenario.Handler.HandleAsync(firstRequest, CancellationToken.None);
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(1, retry.Value?.Revision);
        Assert.Equal(2, second.Value?.Revision);
        Assert.Equal(2, events.Count);
        Assert.Equal(2, Assert.IsType<CriteriaTextOverlayEntryRevised>(events[^1].Event).Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionWithoutWritingGivenOverlayChanged()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var first = await scenario.Handler.HandleAsync(scenario.Request(Uuid.CreateVersion4(),
            expectedRevision: null, Content), CancellationToken.None);
        Assert.True(first.IsSuccess);
        var second = await scenario.Handler.HandleAsync(scenario.Request(Uuid.CreateVersion4(),
            expectedRevision: 1, Content with { Text = "Revised synthetic text" }),
            CancellationToken.None);
        Assert.True(second.IsSuccess);

        // Act
        var stale = await scenario.Handler.HandleAsync(scenario.Request(Uuid.CreateVersion4(),
            expectedRevision: 1, Content with { Text = "Stale synthetic text" }),
            CancellationToken.None);
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ShouldNotWriteGivenUnknownCriterion()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Request(Uuid.CreateVersion4(),
            expectedRevision: null, Content, identifier: "UNKNOWN"), CancellationToken.None);
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(events);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly Uuid _userId = Uuid.CreateVersion4();

        Scenario(ServiceProvider provider, Uuid tenantId)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            TenantId = tenantId;
        }

        public Uuid TenantId { get; }
        public SetCriteriaTextOverlayHandler Handler => new(
            _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), CriteriaCatalog.Platform,
            new FixedTimeProvider(Now));

        public FixedRequestContext Request(Uuid requestId, long? expectedRevision,
            CriteriaTextOverlayContent content, string identifier = "CC6.1") =>
            new(new SetCriteriaTextOverlay(TenantId, CriteriaCatalog.Platform.Edition.EditionId,
                    identifier, expectedRevision, content), Actor(_userId), requestId);

        public static Task<Scenario> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            return Task.FromResult(new Scenario(services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }), Uuid.CreateVersion4()));
        }

        public async Task<List<DomainEventRecord>> ReadEventsAsync()
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _scope.ServiceProvider.GetRequiredService<IEventStore>()
                               .ReadAsync(new EventStreamAddress(TenantId.ToString(),
                                   CriteriaTextOverlayLedger.Area,
                                   CriteriaCatalog.Platform.Edition.EditionId.ToString()), 0,
                                   CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedRequestContext(SetCriteriaTextOverlay request, ClaimsPrincipal actor,
        Uuid requestId) : IRequestContext<SetCriteriaTextOverlay>
    {
        public SetCriteriaTextOverlay Request => request;
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
            new Claim("name", "Synthetic editor")], "UnitTest"));

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
