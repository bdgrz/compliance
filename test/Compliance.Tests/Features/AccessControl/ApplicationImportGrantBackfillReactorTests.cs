using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ApplicationImportGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldGrantStagingToClientRolesGivenNewTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "New client", "new-client"));

        // Act
        await scenario.RunAsync(new ApplicationImportGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        var grants = scenario.SentRequests.OfType<AssignRolePermission>().ToArray();
        Assert.Equal(3, grants.Length);
        Assert.All(grants, grant =>
        {
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(RbacPermissions.ApplicationImportStage, grant.Permission);
            Assert.NotEqual(BuiltInRbac.ViewerRoleId(tenantId), grant.RoleId);
        });
        Assert.Equal(3, grants.Select(grant => grant.RoleId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldGrantImportStagingAndResumeGivenCompletedInventoryBackfill()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var registered = new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme");
        registered.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), tenantId, 1,
            new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)));
        var events = new InMemoryEventStore();
        await events.AppendAsync(new EventStreamAddress("bdgrz", "tenants", tenantId.ToString()),
            0, [registered]);
        var pattern = EventStreamPattern.ForPattern("bdgrz", "tenants");
        var completed = ProjectionCheckpoint.Start;
        await foreach (var record in events.ReadAsync(pattern, completed.Cursor, CancellationToken.None))
            completed = new ProjectionCheckpoint(record.NextCursor);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var inventoryIdentity = new CheckpointIdentity("ApplicationInventoryGrantBackfillV1", pattern);
        await checkpoints.SaveAsync(inventoryIdentity, completed);
        var scenario = new ReactorScenario();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build();
        _ = services.AddCompliance(configuration);
        services.AddSingleton<IProjectionCheckpointStore>(checkpoints);
        services.AddSingleton<IRequestBus>(scenario.Requests);
        var registration = Assert.Single(services
                .Where(descriptor => descriptor.ServiceType == typeof(WorkloadRegistration))
                .Select(descriptor => (WorkloadRegistration)descriptor.ImplementationInstance!),
            workload => workload.Name == "ApplicationImportGrantBackfillV1");
        await using var provider = services.BuildServiceProvider();
        var reactor = Assert.IsAssignableFrom<Reactor>(provider.GetRequiredService(registration.ComponentType));
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var runner = new ReactorRunner(events);

        // Act
        var starting = await checkpoints.LoadAsync(identity);
        await runner.RunAsync(reactor, starting);
        var resumed = await checkpoints.LoadAsync(identity);
        await runner.RunAsync(reactor, resumed);

        // Assert
        Assert.Equal(WorkloadScope.Global, registration.Scope);
        Assert.Equal(ProjectionCheckpoint.Start, starting);
        Assert.Equal(completed, resumed);
        Assert.Equal(completed, await checkpoints.LoadAsync(inventoryIdentity));
        var grants = scenario.SentRequests.OfType<AssignRolePermission>().ToArray();
        Assert.Equal(3, grants.Length);
        Assert.All(grants, grant =>
        {
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(RbacPermissions.ApplicationImportStage, grant.Permission);
        });
        Assert.Equal(new[]
        {
            BuiltInRbac.TenantAdministrationRoleId(tenantId),
            BuiltInRbac.ComplianceManagementRoleId(tenantId),
            BuiltInRbac.ComplianceParticipationRoleId(tenantId),
        }.Order(), grants.Select(grant => grant.RoleId).Order());
    }
}
