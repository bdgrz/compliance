using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ActorReferenceTests
{
    [Fact]
    public async Task ShouldPersistNamedSystemProcessGivenReactorEffect()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build();
        _ = services.AddCompliance(configuration);
        using var provider = services.BuildServiceProvider();
        var principals = provider.GetRequiredService<IReactorPrincipalProvider>();
        var reactor = new NamedReactor(new InMemoryProjectionCheckpointStore());
        var actor = principals.GetPrincipal(reactor);
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var teamId = Uuid.CreateVersion4();
        var context = new RequestContext<DefineTeam>(new DefineTeam(tenantId, teamId,
            "Administrators"), actor);

        // Act
        var result = await new DefineTeamHandler(fixture.Repository)
            .HandleAsync(context, CancellationToken.None);
        DomainEventMetadata? metadata = null;
        await foreach (var record in fixture.Store.ReadAsync(
                           new Team(tenantId, teamId).Stream, 0, CancellationToken.None))
            metadata = record.Event.Metadata;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(RequestActor.IsSystem(actor));
        Assert.Equal(new ActorAttribution("reactor:TenantRbacBootstrap", "bdgrz.system"),
            metadata?.Actor);
        Assert.Equal(ActorReference.ForSystemProcess("reactor:TenantRbacBootstrap",
                "TenantRbacBootstrap"),
            ActorReference.FromSystemMetadata(metadata!));
    }

    [Fact]
    public void ShouldLeaveProcessUnknownGivenLegacyGenericSystemActor()
    {
        // Arrange
        var metadata = new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            1, DateTimeOffset.UtcNow)
        {
            Actor = new ActorAttribution("portia:system", "Portia"),
        };

        // Act
        var actor = ActorReference.FromSystemMetadata(metadata);

        // Assert
        Assert.Null(actor);
    }

    sealed class NamedReactor(IProjectionCheckpointStore checkpoints)
        : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"),
            "TenantRbacBootstrap");
}
