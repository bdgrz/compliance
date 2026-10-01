using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class WorkforcePersonalDetailsGrantBackfillReactorTests
{
    [Fact]
    public void ShouldRegisterIndependentPersonalDetailsBackfillGivenApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();

        // Act
        _ = services.AddCompliance(configuration);
        var workloads = services
            .Where(descriptor => descriptor.ServiceType == typeof(WorkloadRegistration))
            .Select(descriptor => (WorkloadRegistration)descriptor.ImplementationInstance!)
            .ToArray();

        // Assert
        var personalDetails = Assert.Single(workloads,
            workload => workload.Name == "WorkforcePersonalDetailsGrantBackfillV1");
        var managerChain = Assert.Single(workloads,
            workload => workload.Name == "WorkforceRestrictedFieldGrantBackfillV1");
        Assert.NotEqual(managerChain.ComponentType, personalDetails.ComponentType);
        Assert.Equal(WorkloadScope.Global, personalDetails.Scope);
    }

    [Fact]
    public async Task ShouldGrantOnlyPersonalDetailsAndResumeGivenCompletedManagerBackfillCheckpoint()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var registered = new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme");
        registered.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), tenantId, 1,
            new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)));
        var events = new InMemoryEventStore();
        await events.AppendAsync(new EventStreamAddress("bdgrz", "tenants", tenantId.ToString()),
            0, [registered]);
        var pattern = EventStreamPattern.ForPattern("bdgrz", "tenants");
        var completed = ProjectionCheckpoint.Start;
        await foreach (var record in events.ReadAsync(pattern, completed.Cursor, CancellationToken.None))
            completed = new ProjectionCheckpoint(record.NextCursor);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var managerIdentity = new CheckpointIdentity("WorkforceRestrictedFieldGrantBackfillV1", pattern);
        await checkpoints.SaveAsync(managerIdentity, completed);
        var scenario = new ReactorScenario();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();
        _ = services.AddCompliance(configuration);
        services.AddSingleton<IProjectionCheckpointStore>(checkpoints);
        services.AddSingleton<IRequestBus>(scenario.Requests);
        await using var provider = services.BuildServiceProvider();
        var reactor = provider.GetRequiredService<WorkforcePersonalDetailsGrantBackfillReactor>();
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var runner = new ReactorRunner(events);

        // Act
        var starting = await checkpoints.LoadAsync(identity);
        await runner.RunAsync(reactor, starting);
        var resumed = await checkpoints.LoadAsync(identity);
        await runner.RunAsync(reactor, resumed);

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, starting);
        Assert.Equal(completed, resumed);
        Assert.Equal(completed, await checkpoints.LoadAsync(managerIdentity));
        var grants = scenario.SentRequests.OfType<AssignRolePermission>().ToArray();
        Assert.Equal(2, grants.Length);
        Assert.Contains(grants, grant => grant.TenantId == tenantId &&
            grant.RoleId == BuiltInRbac.TenantAdministrationRoleId(tenantId) &&
            grant.Permission == FieldClasses.WorkforcePersonalDetails.ReadPermission);
        Assert.Contains(grants, grant => grant.TenantId == tenantId &&
            grant.RoleId == BuiltInRbac.ComplianceManagementRoleId(tenantId) &&
            grant.Permission == FieldClasses.WorkforcePersonalDetails.ReadPermission);
        Assert.DoesNotContain(grants,
            grant => grant.Permission == FieldClasses.WorkforceManagerChain.ReadPermission);
    }
}
