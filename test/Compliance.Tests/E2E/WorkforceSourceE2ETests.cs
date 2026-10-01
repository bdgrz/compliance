using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class WorkforceSourceE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldRecordAndReconcileAttributedSourceGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner, $"source-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider, $"source-outsider-{Guid.NewGuid():N}@example.com");
        var root = await CreateTenantRootAsync(owner);
        var personId = await RecordPersonAsync(owner, root);
        using var recorded = await owner.PostAsJsonAsync($"{root}/workforce-source-observations", Source(personId));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var observationId = (await ReadAsync(recorded)).GetProperty("observation_id").GetString();
        var path = $"{root}/workforce-source-observations/{observationId}";
        _ = await WaitAsync(owner, $"{path}?minimum_revision=1");

        // Act
        using var previewResponse = await owner.GetAsync($"{path}/preview");
        var preview = await ReadAsync(previewResponse);
        using var accepted = await owner.PutAsJsonAsync($"{path}/decision", Decision());
        using var retry = await owner.PutAsJsonAsync($"{path}/decision", Decision());
        var reconciled = await WaitAsync(owner, $"{path}?minimum_revision=2");
        using var denied = await outsider.GetAsync(path);
        await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
        _ = await mcp.When("bdgrz.workforce.source.preview", new Dictionary<string, object?>
        {
            ["tenant_id"] = root.Split('/')[4],
            ["observation_id"] = observationId,
        }).ExpectSuccess();

        // Assert
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal("authoritative", preview.GetProperty("source_authority").GetString());
        Assert.True(preview.GetProperty("can_accept").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.True(reconciled.GetProperty("accepted_for_current_revision").GetBoolean());
        Assert.Equal("member", reconciled.GetProperty("decision").GetProperty("actor").GetProperty("kind").GetString());
        Assert.Equal("hris", reconciled.GetProperty("source").GetProperty("source_kind").GetString());
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task ShouldRecoverSourceProjectionAndAcceptanceGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-source-split-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = TestHostMode.Current;
        HttpClient client;
        try
        {
            TestHostMode.Set("api");
            client = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner, $"source-split-{Guid.NewGuid():N}@example.com");
        var root = await CreateTenantRootAsync(owner);
        var personId = await RecordPersonAsync(owner, root);
        await worker.StopAsync();

        // Act
        using var recorded = await owner.PostAsJsonAsync($"{root}/workforce-source-observations", Source(personId));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var observationId = (await ReadAsync(recorded)).GetProperty("observation_id").GetString();
        var path = $"{root}/workforce-source-observations/{observationId}";
        using var lagged = await owner.GetAsync($"{path}?minimum_revision=1");
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            _ = await WaitAsync(owner, $"{path}?minimum_revision=1");
            using var accepted = await owner.PutAsJsonAsync($"{path}/decision", Decision());
            var recovered = await WaitAsync(owner, $"{path}?minimum_revision=2");

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            Assert.True(recovered.GetProperty("accepted_for_current_revision").GetBoolean());
            Assert.Equal(1, recovered.GetProperty("decision").GetProperty("target_revision").GetInt64());
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    static object Source(string personId) => new
    {
        source = new { source_kind = "hris", source_system = "people-system", source_record_id = "worker-100", source_revision = "v1" },
        target_kind = "person",
        target_id = personId,
        expected_target_revision = 1,
        observed_at = DateTimeOffset.UtcNow.AddDays(-1),
        facts = new { person = new { display_name = "Ada", work_email = "ada@example.com" } },
    };

    static object Decision() => new { expected_revision = 1, expected_target_revision = 1, outcome = "accepted", note = "HRIS source checked" };

    static async Task<string> CreateTenantRootAsync(HttpClient client)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Source tenant",
            slug = $"source-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return $"/api/v1/tenants/{(await ReadAsync(created)).GetProperty("tenant_id").GetString()}";
    }

    static async Task<string> RecordPersonAsync(HttpClient client, string root)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var created = await client.PostAsJsonAsync($"{root}/people",
                new { display_name = "Ada", work_email = "ada@example.com" });
            if (created.StatusCode == HttpStatusCode.OK)
            {
                var personId = (await ReadAsync(created)).GetProperty("person_id").GetString()!;
                _ = await WaitAsync(client, $"{root}/people/{personId}?minimum_revision=1");
                return personId;
            }
            Assert.True(created.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                await created.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Source person recording never became authorized.");
    }

    static async Task<JsonElement> WaitAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} did not catch up.");
    }

    IHost BuildWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();
}
