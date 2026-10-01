using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProviderRegisterE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainAuthoredHistoryGivenProviderRevisionInStandaloneOrSplitHost(bool split)
    {
        // Arrange
        var applicationName = $"compliance-provider-{Guid.NewGuid():N}";
        using var worker = split ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = TestHostMode.Current;
        HttpClient client;
        try
        {
            TestHostMode.Set(split ? "api" : "standalone");
            client = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner, $"provider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/providers";
        var registration = await RecordWhenAuthorizedAsync(owner, path);
        var exact = $"{path}/{registration.ProviderId}";
        await WaitForRevisionAsync(owner, exact, 1);
        if (worker is not null)
            await worker.StopAsync();

        // Act
        using var revised = await owner.PutAsJsonAsync(exact, new
        {
            expected_revision = 1,
            content = new { name = "Revised supplier", provider_kind = "vendor" },
        });
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        if (split)
        {
            using var lagging = await owner.GetAsync($"{exact}?minimum_revision=2");
            Assert.Equal(HttpStatusCode.Conflict, lagging.StatusCode);
            Assert.Equal("true", lagging.Headers.GetValues("Portia-Transient").Single());
        }
        using var restartedWorker = split ? BuildWorker(applicationName) : null;
        if (restartedWorker is not null)
            await restartedWorker.StartAsync();
        await WaitForRevisionAsync(owner, exact, 2);

        // Assert
        using var historical = await owner.GetAsync($"{exact}/revisions/1");
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        var original = await historical.Content.ReadFromJsonAsync<ProviderDocument>();
        Assert.Equal(1, original!.Revision);
        Assert.Equal("Original supplier", original.Content.Name);
        using var currentResponse = await owner.GetAsync(exact);
        var current = await currentResponse.Content.ReadFromJsonAsync<ProviderDocument>();
        Assert.Equal(2, current!.Revision);
        Assert.Equal("Revised supplier", current.Content.Name);
        await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
        var mcpHistory = await mcp.When("bdgrz.provider.revision.get", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["provider_id"] = registration.ProviderId,
            ["revision"] = 1,
        }).ExpectSuccess();
        Assert.Equal(1, Assert.IsType<JsonElement>(mcpHistory.StructuredJson)
            .GetProperty("result").GetProperty("revision").GetInt64());
        var otherTenantId = await CreateTenantAsync(owner);
        var otherPath = $"/api/v1/tenants/{otherTenantId}/providers";
        await RecordWhenAuthorizedAsync(owner, otherPath);
        using var foreign = await owner.GetAsync($"{otherPath}/{registration.ProviderId}");
        using var foreignHistory = await owner.GetAsync($"{otherPath}/{registration.ProviderId}/revisions/1");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignHistory.StatusCode);
        if (restartedWorker is not null)
            await restartedWorker.StopAsync();
    }

    IHost BuildWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static async Task<Guid> CreateTenantAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Provider tenant",
            slug = $"provider-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TenantDocument>())!.TenantId;
    }

    static async Task<RegistrationDocument> RecordWhenAuthorizedAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, new
            {
                content = new { name = "Original supplier", provider_kind = "vendor" },
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await response.Content.ReadFromJsonAsync<RegistrationDocument>())!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Default provider management grant was not projected.");
    }

    static async Task WaitForRevisionAsync(HttpClient client, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var view = await response.Content.ReadFromJsonAsync<ProviderDocument>();
                Assert.Equal(revision, view!.Revision);
                return;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"Provider revision {revision} was not projected.");
    }

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] Guid TenantId);
    sealed record RegistrationDocument([property: JsonPropertyName("provider_id")] Guid ProviderId);
    sealed record ProviderDocument(long Revision, ContentDocument Content);
    sealed record ContentDocument(string Name);
}
