using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class AccessReviewScopeLegacyAndProjectionTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordDecisionGivenLegacyApplicationStreamInstance()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(registrantId: Uuid.CreateVersion4());
        var decide = scenario.DecideHandler();
        var get = new GetAccessReviewScopeHandler(scenario.Reader, scenario.Events,
            TimeProvider.System);

        // Act
        var decided = await decide.HandleAsync(scenario.DecideContext(Uuid.CreateVersion4()),
            CancellationToken.None);
        var read = await get.HandleAsync(new RequestContext<GetAccessReviewScope>(
            new GetAccessReviewScope(scenario.TenantId, scenario.ApplicationId,
                scenario.InstanceId, Now.AddDays(1)), Principal(Uuid.CreateVersion4())),
            CancellationToken.None);

        // Assert
        Assert.True(decided.IsSuccess);
        Assert.Equal(1, decided.Value.SystemInstanceRevision);
        Assert.True(read.IsSuccess);
        Assert.Equal("included", read.Value.Status);
    }

    [Fact]
    public async Task ShouldProhibitLegacyRegistrantGivenNoWaiver()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        await using var scenario = await Scenario.CreateAsync(registrantId: null, userId);
        var decide = scenario.DecideHandler();

        // Act
        var result = await decide.HandleAsync(scenario.DecideContext(userId),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenLegacyInstanceOfAnotherApplication()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(registrantId: Uuid.CreateVersion4());
        var decide = scenario.DecideHandler();
        var context = new RequestContext<DecideAccessReviewScope>(new DecideAccessReviewScope(
            scenario.TenantId, Uuid.CreateVersion4(), scenario.InstanceId, 1, 0, "included",
            "Production", Now), Principal(Uuid.CreateVersion4()));

        // Act
        var result = await decide.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldProjectSupersedingHistoryIdempotentlyGivenReplayedDecisions()
    {
        // Arrange
        var directory = new FitzAccessReviewScopeDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var first = Decided(tenantId, applicationId, instanceId, 1, "included", Now);
        var second = Decided(tenantId, applicationId, instanceId, 2, "excluded", Now.AddDays(10));

        // Act
        await ProjectAsync(directory, tenantId, first, second, first, second);
        var record = await directory.GetAsync(tenantId, instanceId);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), instanceId);

        // Assert
        Assert.NotNull(record);
        Assert.Equal([1L, 2L], record.Decisions.Select(decision => decision.Sequence));
        Assert.Null(alien);
    }

    [Fact]
    public async Task ShouldRejectOutOfOrderDecisionGivenMissingPredecessor()
    {
        // Arrange
        var directory = new FitzAccessReviewScopeDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var gap = Decided(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2,
            "included", Now);

        // Act
        var failure = await Record.ExceptionAsync(() => ProjectAsync(directory, tenantId, gap));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void ShouldReportEffectiveUpcomingAndOverdueGivenDatedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var instance = new SystemInstanceView(tenantId, applicationId, instanceId, "AWS",
            "cloud_account", null, "manual", null, [], Uuid.CreateVersion4(), "Registrant", Now);
        var history = new[]
        {
            View(Decided(tenantId, applicationId, instanceId, 1, "included", Now,
                Now.AddDays(5))),
            View(Decided(tenantId, applicationId, instanceId, 2, "excluded", Now.AddDays(30))),
        };

        // Act
        var before = AccessReviewScopeStatus.Evaluate(instance, history, Now.AddDays(-1));
        var overdue = AccessReviewScopeStatus.Evaluate(instance, history, Now.AddDays(6));
        var later = AccessReviewScopeStatus.Evaluate(instance, history, Now.AddDays(31));

        // Assert
        Assert.Equal("unresolved", before.Status);
        Assert.Equal(1, before.Upcoming?.Sequence);
        Assert.Equal("included", overdue.Status);
        Assert.True(overdue.ReviewOverdue);
        Assert.Equal(2, overdue.Upcoming?.Sequence);
        Assert.Equal("excluded", later.Status);
        Assert.False(later.ReviewOverdue);
        Assert.Null(later.Upcoming);
    }

    static async Task ProjectAsync(FitzAccessReviewScopeDirectory directory, Uuid tenantId,
        params AccessReviewScopeDecided[] events)
    {
        var identity = new CheckpointIdentity("AccessReviewScopeDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(),
                "system-instance-access-review-scopes"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var ev in events)
            await directory.ApplyAsync(ev);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static AccessReviewScopeDecided Decided(Uuid tenantId, Uuid applicationId, Uuid instanceId,
        long sequence, string decision, DateTimeOffset effectiveFrom,
        DateTimeOffset? reviewBy = null) =>
        new(tenantId, applicationId, instanceId, 1, Uuid.CreateVersion4(), sequence, decision,
            "Reason", effectiveFrom, reviewBy, Uuid.CreateVersion4(), "Lead", Now, null);

    static AccessReviewScopeDecisionView View(AccessReviewScopeDecided ev) =>
        new(ev.TenantId, ev.ApplicationId, ev.SystemInstanceId, ev.SystemInstanceRevision,
            ev.DecisionId, ev.Sequence, ev.Decision, ev.Reason, ev.EffectiveFrom, ev.ReviewBy,
            ev.Actor, ev.DecidedAt, null);

    static ClaimsPrincipal Principal(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        Scenario(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
        }

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ApplicationId { get; } = Uuid.CreateVersion4();
        public Uuid InstanceId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();

        /// <summary>Declares a legacy instance; a null registrant means the member of <paramref name="userId"/>.</summary>
        public static async Task<Scenario> CreateAsync(Uuid? registrantId, Uuid? userId = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var scenario = new Scenario(services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }));
            var actor = registrantId ?? RbacIds.Member(scenario.TenantId, userId!.Value);
            var stream = new EventStreamAddress(scenario.TenantId.ToString(), "applications",
                scenario.ApplicationId.ToString());
            await scenario.Events.AppendAsync(stream, 0,
            [
                DomainEventSeed.Attach(new ApplicationDeclared(scenario.TenantId,
                    scenario.ApplicationId, "Payroll", "Run payroll", null, actor, "Manager",
                    Now), scenario.ApplicationId, 1),
                DomainEventSeed.Attach(new SystemInstanceDeclared(scenario.TenantId,
                    scenario.ApplicationId, scenario.InstanceId, 2, "Production", "production",
                    null, "payroll-prod", actor, "Manager", Now), scenario.ApplicationId, 2),
            ]);
            return scenario;
        }

        public DecideAccessReviewScopeHandler DecideHandler() => new(
            _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), Reader, Events,
            TimeProvider.System);

        public RequestContext<DecideAccessReviewScope> DecideContext(Uuid userId) => new(
            new DecideAccessReviewScope(TenantId, ApplicationId, InstanceId, 1, 0, "included",
                "Production data", Now), Principal(userId));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
