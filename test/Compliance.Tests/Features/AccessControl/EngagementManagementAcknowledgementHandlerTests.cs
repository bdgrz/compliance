using System.Security.Claims;
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
