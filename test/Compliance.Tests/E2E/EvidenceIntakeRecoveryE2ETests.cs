using System.Net;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class EvidenceIntakeRecoveryE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>, IDisposable
{
    const string DeliveryKey = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=";
    readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-evidence-recovery-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldResumeDurablePendingEvidenceGivenFreshStandaloneOrSplitHostScope(bool splitHost)
    {
        // Arrange
        var applicationName = $"compliance-evidence-recovery-{Guid.NewGuid():N}";
        using var worker = splitHost ? CreateWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host =>
                {
                    host.UseSetting("COMPLIANCE_HOST_MODE", splitHost ? "api" : "standalone");
                    host.UseSetting("Artifacts:LocalContentRoot", _root);
                    host.UseSetting("Artifacts:LocalDeliveryKey", DeliveryKey);
                });
            using var client = factory.CreateClient();
            using var ready = await client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            var tenantId = Uuid.CreateVersion4();
            var bytes = "recoverable evidence"u8.ToArray();
            var artifactId = EvidenceIntake.IdFor(tenantId, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var collector = ActorReference.ForMember(Uuid.CreateVersion4(), "Original collector");
            var metadata = new EvidenceArtifactContent("Original evidence", null, "document", "Manual capture",
                DateTimeOffset.UtcNow, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "restricted");

            // Act
            await using (var first = factory.Services.CreateAsyncScope())
            {
                var services = first.ServiceProvider;
                var store = services.GetRequiredService<IArtifactContentStore>();
                var intake = Intake(services, new Inspector(store, fail: true));
                await Assert.ThrowsAsync<IOException>(() => intake.CaptureAsync(tenantId, new MemoryStream(bytes),
                    metadata, collector, new RequestDispatchContext(RequestActor.System), CancellationToken.None)
                    .AsTask());
            }
            await using var recovery = (worker?.Services ?? factory.Services).CreateAsyncScope();
            var reader = recovery.ServiceProvider.GetRequiredService<IAggregateReader>();
            var pending = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId));
            var recovered = await Intake(recovery.ServiceProvider,
                    new Inspector(recovery.ServiceProvider.GetRequiredService<IArtifactContentStore>(), fail: false))
                .CaptureAsync(tenantId, new MemoryStream(bytes), metadata with { Title = "Retry title" },
                    ActorReference.ForMember(Uuid.CreateVersion4(), "Retry collector"),
                    new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            var completed = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId));
            var otherTenant = await reader.HydrateAsync(new EvidenceArtifact(Uuid.CreateVersion4(), artifactId));

            // Assert
            Assert.True(pending.IsCreated);
            Assert.Equal(EvidenceArtifactStates.PendingInspection, pending.State);
            Assert.True(recovered.IsSuccess);
            Assert.True(recovered.Value.Duplicate);
            Assert.Equal(artifactId, recovered.Value.ArtifactId);
            Assert.Equal(EvidenceArtifactStates.Available, completed.State);
            Assert.Equal(metadata, completed.Content);
            Assert.False(otherTenant.IsCreated);
        }
        finally
        {
            if (worker is not null)
                await worker.StopAsync();
        }
    }

    IHost CreateWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Configuration["Artifacts:LocalContentRoot"] = _root;
        builder.Configuration["Artifacts:LocalDeliveryKey"] = DeliveryKey;
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static EvidenceIntake Intake(IServiceProvider services, IArtifactInspector inspector) =>
        new(services.GetRequiredService<IArtifactContentStore>(), inspector,
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<TimeProvider>());

    sealed class Inspector(IArtifactContentStore store, bool fail) : IArtifactInspector
    {
        public async ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
            CancellationToken ct = default)
        {
            if (fail)
                throw new IOException("The scanner is unavailable.");
            await using var opened = await store.OpenVerifiedAsync(content, ct);
            if (opened is null)
                throw new IOException("The uploaded content is unavailable.");
            return new ArtifactInspectionResult(ArtifactInspectionState.Clean);
        }
    }
}
