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
public sealed class PersonE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldRecordReviseAndIsolatePeopleGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"person-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"person-outsider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var otherTenantId = await CreateTenantAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/people";

        // Act
        var personId = await RecordWhenAuthorizedAsync(owner, path, new
        {
            display_name = "Ada Lovelace",
            work_email = "ada@example.com",
        });
        var personPath = $"{path}/{personId}";
        var original = await WaitForRevisionAsync(owner, personPath, 1);
        using var revised = await owner.PutAsJsonAsync(personPath, new
        {
            expected_revision = 1,
            display_name = "Ada King",
            work_email = "ada@example.com",
        });
        using var stale = await owner.PutAsJsonAsync(personPath, new
        {
            expected_revision = 1,
            display_name = "Stale",
        });
        using var invalid = await owner.PostAsJsonAsync(path, new
        {
            display_name = "Nobody",
            work_email = "not-an-email",
        });
        var current = await WaitForRevisionAsync(owner, personPath, 2);
        using var listed = await owner.GetAsync($"{path}?limit=1");
        using var invalidLimit = await owner.GetAsync($"{path}?limit=201");
        using var invalidCursor = await owner.GetAsync($"{path}?cursor=invalid");
        using var outsiderRead = await outsider.GetAsync(personPath);
        using var outsiderList = await outsider.GetAsync(path);
        using var outsiderWrite = await outsider.PostAsJsonAsync(path,
            new { display_name = "Intruder" });
        using var otherTenantRead = await owner.GetAsync(
            $"/api/v1/tenants/{otherTenantId}/people/{personId}");

        // Assert
        Assert.Equal("Ada Lovelace", original.GetProperty("display_name").GetString());
        Assert.Equal("manual", original.GetProperty("source_kind").GetString());
        Assert.Equal("member", original.GetProperty("last_changed_by")
            .GetProperty("kind").GetString());
        Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("Ada King", current.GetProperty("display_name").GetString());
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(personId, (await ReadAsync(listed)).GetProperty("items")[0]
            .GetProperty("person_id").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderWrite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherTenantRead.StatusCode);
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.workforce.person.record", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["display_name"] = "Grace Hopper",
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.workforce.person.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["person_id"] = personId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.workforce.person.revise", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["person_id"] = personId,
                ["expected_revision"] = 2,
                ["display_name"] = "Augusta Ada King",
            }).ExpectSuccess();
        }
        var third = await WaitForRevisionAsync(owner, personPath, 3);
        Assert.Equal("Augusta Ada King", third.GetProperty("display_name").GetString());
        Assert.Equal(JsonValueKind.Null, third.GetProperty("work_email").ValueKind);
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.workforce.person.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["person_id"] = personId,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.workforce.person.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
            }).ExpectFailure();
        }
    }

    [Fact]
    public async Task ShouldReturnTransientLagAndRecoverGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-person-split-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        HttpClient client;
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"person-split-owner-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/people";
        var firstId = await RecordWhenAuthorizedAsync(owner, path,
            new { display_name = "Ada Lovelace" });
        _ = await WaitForRevisionAsync(owner, $"{path}/{firstId}", 1);
        await worker.StopAsync();

        // Act
        using var created = await owner.PostAsJsonAsync(path,
            new { display_name = "Grace Hopper" });
        var personId = (await ReadAsync(created)).GetProperty("person_id").GetString();
        using var laggedGet = await owner.GetAsync($"{path}/{personId}");
        using var laggedList = await owner.GetAsync(path);
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var projected = await WaitForRevisionAsync(owner, $"{path}/{personId}", 1);
            using var listed = await owner.GetAsync(path);

            // Assert
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, laggedGet.StatusCode);
            Assert.Equal("true", laggedGet.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, laggedList.StatusCode);
            Assert.Equal("true", laggedList.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("Grace Hopper", projected.GetProperty("display_name").GetString());
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            Assert.Equal(2, (await ReadAsync(listed)).GetProperty("items").GetArrayLength());
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    static async Task<Guid> CreateTenantAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Workforce tenant",
            slug = $"people-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        return Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
    }

    /// <summary>Retries until the tenant bootstrap and workforce grant backfill have landed.</summary>
    static async Task<string> RecordWhenAuthorizedAsync(HttpClient owner, string path,
        object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty("person_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Recording a person never became authorized after tenant bootstrap.");
    }

    static async Task<JsonElement> WaitForRevisionAsync(HttpClient client, string path,
        int revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("The person projection did not catch up.");
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
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true)
            .AddWorkers();
        return builder.Build();
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
