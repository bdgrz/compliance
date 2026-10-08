using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementManagementAcknowledgementHandlerTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("system")]
    public async Task ShouldDenyPersonalAcknowledgementGivenNonHttpOrSystemInvocation(string invocation)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var request = new AcknowledgeEngagementManagement(tenant, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            0, 1, [], "I retain management responsibility.");
        var actor = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", invocation == "system" ? "bdgrz.system" : "bdgrz"),
            new Claim("sub", Uuid.CreateVersion4().ToString())], "Synthetic"));
        RequestInvocation transport = invocation switch
        {
            "mcp" => new McpInvocation("synthetic-tool"),
            "system" => new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"),
            _ => new DirectInvocation()
        };
        var handler = new AcknowledgeEngagementManagementHandler(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(new Context(request, actor, transport), CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new IndependenceLedger(tenant));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, retained.Sequence);
        Assert.Empty(retained.ManagementAcknowledgements(request.EngagementId));
    }

    [Fact]
    public async Task ShouldPersistPersonalDecisionGivenHttpInvocationAndCurrentClientManagementAuthority()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var engagement = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));
        var attribution = ActorReference.ForMember(RbacIds.Member(tenant, user), "Synthetic client administrator");
        var now = DateTimeOffset.UtcNow;
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest",
            "Synthetic source", true, 1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), now);
        Assert.True((await executor.ExecuteAsync(new IndependenceLedger(tenant), ledger => AggregateOutcome.CommitOnSuccess(
            ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, 0, new ServiceEngagementDraftContent(
                "attest", "Synthetic scope", new DateOnly(2026, 1, 1), null, staff.StaffMemberId), staff, attribution, now)),
            new RequestDispatchContext(actor))).IsSuccess);
        Assert.True((await executor.ExecuteAsync(new IndependenceLedger(tenant), ledger => AggregateOutcome.CommitOnSuccess(
            ledger.RecordService(Uuid.CreateVersion4(), serviceId, 1, new NonattestServiceContent(engagement,
                "readiness", new DateOnly(2026, 1, 1), null, [staff.StaffMemberId], false, "Synthetic source"), attribution, now)),
            new RequestDispatchContext(actor))).IsSuccess);
        var request = new AcknowledgeEngagementManagement(tenant, engagement, Uuid.CreateVersion4(), 2, 1,
            [serviceId], "I retain management responsibility.");
        var authorizer = new IndependenceAdministrationAuthorizer(new RecordingPermissionAuthorizer(true),
            new ActiveTenant(), new AlwaysMemberDirectory());
        var authorized = await authorizer.AuthorizeAsync(new RequestContext<IIndependenceAdministrationRequest>(request, actor),
            CancellationToken.None);
        Assert.True(authorized.IsSuccess);
        var handler = new AcknowledgeEngagementManagementHandler(executor, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(new Context(request, actor,
            new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic")), CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new IndependenceLedger(tenant));

        // Assert
        Assert.True(result.IsSuccess);
        var ack = Assert.Single(retained.ManagementAcknowledgements(engagement));
        Assert.Equal(user, ack.UserId);
        Assert.Equal(RbacIds.Member(tenant, user).ToString(), ack.Actor.Id);
        Assert.Equal(serviceId, Assert.Single(ack.CompleteServiceRecordIds));
        Assert.Equal(serviceId, Assert.Single(retained.History().Services).ServiceRecordId);
        Assert.Equal(3, retained.Sequence);
        Assert.False(retained.Engagement(engagement)!.ProfessionalAccessGranted);
    }

    sealed class Context(AcknowledgeEngagementManagement request, ClaimsPrincipal actor, RequestInvocation invocation)
        : IRequestContext<AcknowledgeEngagementManagement>
    {
        public AcknowledgeEngagementManagement Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt => DateTimeOffset.UtcNow;
        public RequestInvocation Invocation => invocation;
    }
}
