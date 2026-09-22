using System.Net;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ArtifactContentHostModeE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>, IDisposable
{
    const string DeliveryKey = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=";
    readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-artifacts-e2e-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ShouldStoreVerifyAndDeliverContentGivenStandaloneHost()
    {
        // Arrange
        await using var factory = Configure(E2EAppFactory.Create(broker));
        using var http = factory.CreateClient();
        using var ready = await http.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var store = factory.Services.GetRequiredService<IArtifactContentStore>();
        var inspector = factory.Services.GetRequiredService<IArtifactInspector>();
        var tenantId = Uuid.CreateVersion4();
        var bytes = "standalone evidence"u8.ToArray();

        // Act
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
        var inspection = await inspector.InspectAsync(written.Content);
        var delivery = await store.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
        await using var delivered = await Assert.IsType<LocalArtifactContentStore>(store)
            .OpenDeliveryAsync(delivery!.Location);

        // Assert
        Assert.False(written.AlreadyPresent);
        Assert.Equal(ArtifactInspectionState.NotInspected, inspection.State);
        Assert.NotNull(delivered);
        Assert.Equal(bytes, await ReadAllAsync(delivered));
    }

    [Fact]
    public async Task ShouldShareTenantScopedContentGivenSplitApiAndWorkerHosts()
    {
        // Arrange
        var applicationName = $"compliance-split-artifacts-{Guid.NewGuid():N}";
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
        using var worker = builder.Build();
        await worker.StartAsync();
        try
        {
            await using var factory = Configure(E2EAppFactory.Create(broker, applicationName));
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient http;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                http = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }

            using var client = http;
            using var ready = await client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            var apiStore = factory.Services.GetRequiredService<IArtifactContentStore>();
            var workerStore = worker.Services.GetRequiredService<IArtifactContentStore>();
            var tenantA = Uuid.CreateVersion4();
            var tenantB = Uuid.CreateVersion4();
            var bytes = "split-host evidence"u8.ToArray();

            // Act
            var written = await apiStore.StoreIfAbsentAsync(tenantA, new MemoryStream(bytes));
            await using var workerRead = await workerStore.OpenVerifiedAsync(written.Content);
            await using var crossTenant = await workerStore.OpenVerifiedAsync(
                written.Content with { TenantId = tenantB });
            var repeated = await workerStore.StoreIfAbsentAsync(tenantA, new MemoryStream(bytes));
            var tenantBWrite = await workerStore.StoreIfAbsentAsync(tenantB, new MemoryStream(bytes));
            var delivery = await apiStore.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
            await using var delivered = await Assert.IsType<LocalArtifactContentStore>(workerStore)
                .OpenDeliveryAsync(delivery!.Location);

            // Assert
            Assert.NotNull(workerRead);
            Assert.Equal(bytes, await ReadAllAsync(workerRead));
            Assert.Null(crossTenant);
            Assert.True(repeated.AlreadyPresent);
            Assert.False(tenantBWrite.AlreadyPresent);
            Assert.NotNull(delivered);
            Assert.Equal(bytes, await ReadAllAsync(delivered));
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    WebApplicationFactory<Program> Configure(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(host =>
        {
            host.UseSetting("Artifacts:LocalContentRoot", _root);
            host.UseSetting("Artifacts:LocalDeliveryKey", DeliveryKey);
        });

    static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
