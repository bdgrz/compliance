using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class RiskAcceptanceWorkCompositionTests
{
    [Fact]
    public async Task ShouldDispatchQueueAndAcceptanceThroughAuthorizationGivenProductionComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build();
        _ = services.AddCompliance(configuration, developerAuthentication: true);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var work = Uuid.CreateVersion4();

        // Act
        var list = await bus.SendAsync(new ListWork(tenant, program), RequestActor.Anonymous);
        var get = await bus.SendAsync(new GetWorkItem(tenant, program, work), RequestActor.Anonymous);
        var assign = await bus.SendAsync(new AssignWorkItem(tenant, program, work, 0, Uuid.CreateVersion4()),
            RequestActor.Anonymous);
        var accept = await bus.SendAsync(new AcceptRisk(tenant, program, Uuid.CreateVersion4(), 3,
            Uuid.CreateVersion4(), "compliance_lead", DateTimeOffset.UtcNow.AddMonths(6), "Recorded rationale"),
            RequestActor.Anonymous);

        // Assert
        Assert.Equal(RequestErrorKind.Unauthorized, list.Error!.Kind);
        Assert.Equal(RequestErrorKind.Unauthorized, get.Error!.Kind);
        Assert.Equal(RequestErrorKind.Unauthorized, assign.Error!.Kind);
        Assert.Equal(RequestErrorKind.Unauthorized, accept.Error!.Kind);
    }
}
