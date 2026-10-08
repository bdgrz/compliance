using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceOperationTests
{
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly IndependenceRuleContent Rules = new(12,
        [new("design", "impairing", "impairing")], "Synthetic source");
    static ClaimsPrincipal Actor => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldReturnTransientConflictGivenDraftRuleSourceChangesDuringEvaluation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var catalog = new IndependenceRuleCatalog();
        Assert.True(catalog.ReviseRules(Uuid.CreateVersion4(), 0, Rules,
            ActorReference.ForPlatformOperator(User, "Synthetic operator"), DateTimeOffset.UtcNow).IsSuccess);
        var reader = new ChangingCatalogReader(catalog);
        var handler = new EvaluateClientIndependenceHandler(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), reader, TimeProvider.System);
        var request = new EvaluateClientIndependence(Tenant, Uuid.CreateVersion4(), 0, 1,
            new DateOnly(2026, 10, 7));

        // Act
        var result = await handler.HandleAsync(new RequestContext<EvaluateClientIndependence>(request, Actor),
            CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new IndependenceLedger(Tenant));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        var preview = Assert.Single(retained.History().Evaluations);
        Assert.Equal(1, preview.RuleSetVersion);
        Assert.True(preview.ProductionAcceptanceBlocked);
        Assert.Equal(2, reader.Reads);
    }

    [Theory]
    [InlineData(true, true, false, false, "client_personnel", true)]
    [InlineData(false, true, false, false, "client_personnel", false)]
    [InlineData(true, false, false, false, "client_personnel", false)]
    [InlineData(true, true, true, false, "client_personnel", false)]
    [InlineData(true, true, false, true, "client_personnel", false)]
    [InlineData(true, true, false, false, "firm_staff", false)]
    public async Task ShouldRequireExplicitClientAdministrationGivenMembershipState(bool permitted, bool member,
        bool suspended, bool deprovisioned, string affiliation, bool allowed)
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(permitted);
        var authorizer = new IndependenceAdministrationAuthorizer(permissions, new ActiveTenant(),
            new FixedMembershipDirectory(member, affiliation, suspended, deprovisioned));
        var request = new GetClientIndependenceHistory(Tenant);
        var context = new RequestContext<IIndependenceAdministrationRequest>(request, Actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        if (allowed)
        {
            Assert.Equal(RbacPermissions.TenantRbacManage, Assert.Single(permissions.Permissions));
            Assert.Equal(RbacIds.Member(Tenant, User), Assert.Single(permissions.MemberIds));
        }
    }

    [Fact]
    public async Task ShouldDenySystemAuthorityGivenClientBusinessHistoryRequest()
    {
        // Arrange
        var authorizer = new IndependenceAdministrationAuthorizer(new RecordingPermissionAuthorizer(true),
            new ActiveTenant(), new FixedMembershipDirectory(true));
        var context = new RequestContext<IIndependenceAdministrationRequest>(
            new GetClientIndependenceHistory(Tenant), RequestActor.System);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error?.Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRequireCurrentOperatorGivenDraftRuleAdministration(bool isOperator)
    {
        // Arrange
        var authorizer = new IndependenceRuleAdministrationAuthorizer(new OperatorAccess(isOperator));
        var context = new RequestContext<IIndependenceRuleAdministrationRequest>(
            new ReviseIndependenceRules(0, Rules), Actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(isOperator, result.IsSuccess);
    }

    [Fact]
    public async Task ShouldDenySystemAuthorityGivenDraftRuleAdministration()
    {
        // Arrange
        var authorizer = new IndependenceRuleAdministrationAuthorizer(new OperatorAccess(true));
        var context = new RequestContext<IIndependenceRuleAdministrationRequest>(
            new ReviseIndependenceRules(0, Rules), RequestActor.System);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error?.Kind);
    }

    sealed class OperatorAccess(bool allowed) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);
    }

    sealed class ChangingCatalogReader(IndependenceRuleCatalog catalog) : IAggregateReader
    {
        public int Reads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Assert.IsType<IndependenceRuleCatalog>(aggregate);
            if (++Reads == 2)
                Assert.True(catalog.ReviseRules(Uuid.CreateVersion4(), 1,
                    Rules with { LookBackMonths = 24 }, ActorReference.ForPlatformOperator(User,
                        "Synthetic operator"), DateTimeOffset.UtcNow).IsSuccess);
            return ValueTask.FromResult((TAggregate)(Aggregate)catalog);
        }
    }
}
