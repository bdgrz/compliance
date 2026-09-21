using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class RiskDraftE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldExposeFlatMcpWritesAndPreserveHistoryGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"risk-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"risk-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/risks";
        using var emptyResponse = await owner.GetAsync(path);

        // Act
        using var createdResponse = await owner.PostAsJsonAsync(path, Draft("r-01"));
        var registration = await ReadAsync(createdResponse);
        var riskId = registration.GetProperty("risk_id").GetString();
        var draftPath = $"{path}/{riskId}/draft";
        using var duplicate = await owner.PostAsJsonAsync(path, Draft("R-01"));
        var original = await WaitForRevisionAsync(owner, draftPath, 1);
        using var revisedResponse = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            title = "Provider outage",
            scenario = "Provider becomes unavailable",
            potential_effect = "Service requests cannot be processed",
            source_note = "Management observation",
        });
        using var staleResponse = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            title = "Stale",
            scenario = "Stale edit",
            potential_effect = "Unknown",
        });
        var revised = await WaitForRevisionAsync(owner, draftPath, 2);
        using var exact = await owner.GetAsync($"{draftPath}/revisions/1");
        using var outsiderRead = await outsider.GetAsync(draftPath);
        using var outsiderList = await outsider.GetAsync(path);
        using var outsiderWrite = await outsider.PostAsJsonAsync(path, Draft("R-02"));
        using var otherProgram = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{Guid.NewGuid()}/risks/{riskId}/draft");
        using var invalidLimit = await owner.GetAsync($"{path}?limit=201");
        using var invalidCursor = await owner.GetAsync($"{path}?cursor=invalid");
        using var listed = await owner.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        Assert.Empty((await ReadAsync(emptyResponse)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        Assert.Equal("R-01", registration.GetProperty("identifier").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("draft_unassessed", original.GetProperty("status").GetString());
        Assert.Equal("unresolved", original.GetProperty("owner_resolution").GetString());
        Assert.Equal(HttpStatusCode.NoContent, revisedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal("Provider becomes unavailable", revised.GetProperty("content")
            .GetProperty("scenario").GetString());
        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
        Assert.Equal("Provider stops responding", (await ReadAsync(exact))
            .GetProperty("content").GetProperty("scenario").GetString());
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderWrite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherProgram.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        Assert.Single((await ReadAsync(listed)).GetProperty("items").EnumerateArray());
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.risk.draft.create", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["identifier"] = "R-02",
                ["title"] = "Change failure",
                ["scenario"] = "A release fails",
                ["potential_effect"] = "Users cannot access the service",
                ["source_note"] = "Management observation",
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.risk.draft.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["risk_id"] = riskId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.risk.draft.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.risk.draft.revision.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["risk_id"] = riskId,
                ["revision"] = 1,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.risk.draft.revise", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["risk_id"] = riskId,
                ["expected_revision"] = 2,
                ["title"] = "Provider outage",
                ["scenario"] = "Provider remains unavailable",
                ["potential_effect"] = "Service requests cannot be processed",
                ["source_note"] = "Management observation",
            }).ExpectSuccess();
        }
        var third = await WaitForRevisionAsync(owner, draftPath, 3);
        Assert.Equal("Provider remains unavailable", third.GetProperty("content")
            .GetProperty("scenario").GetString());
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.risk.draft.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["risk_id"] = riskId,
            }).ExpectFailure();
        }
        using var openApiResponse = await owner.GetAsync("/openapi/v1.json");
        var openApi = await ReadAsync(openApiResponse);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var collection = openApi.GetProperty("paths").GetProperty(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks");
        var body = collection.GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("properties");
        Assert.True(body.TryGetProperty("potential_effect", out _));
        Assert.False(body.TryGetProperty("tenant_id", out _));
    }

    [Fact]
    public async Task ShouldReturnTransientLagAndRecoverGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-risk-split-{Guid.NewGuid():N}";
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
            $"risk-split-owner-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/risks";
        await worker.StopAsync();

        // Act
        using var created = await owner.PostAsJsonAsync(path, Draft("R-01"));
        var riskId = (await ReadAsync(created)).GetProperty("risk_id").GetString();
        var draftPath = $"{path}/{riskId}/draft";
        using var laggedGet = await owner.GetAsync($"{draftPath}?minimum_revision=1");
        using var laggedList = await owner.GetAsync(path);
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var projected = await WaitForRevisionAsync(owner, draftPath, 1);
            using var listed = await owner.GetAsync(path);

            // Assert
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, laggedGet.StatusCode);
            Assert.Equal("true", laggedGet.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, laggedList.StatusCode);
            Assert.Equal("true", laggedList.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("draft_unassessed", projected.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    static object Draft(string identifier) => new
    {
        identifier,
        title = "Provider outage",
        scenario = "Provider stops responding",
        potential_effect = "Service requests cannot be processed",
        source_note = "Management observation",
    };
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

    static async Task<(Guid TenantId, Guid ProgramId)> CreateProgramAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Risk draft tenant",
            slug = $"risk-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        var tenantId = Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
        return (tenantId, await CreateProgramForTenantAsync(owner, tenantId));
    }

    static async Task<Guid> CreateProgramForTenantAsync(HttpClient owner, Guid tenantId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var program = await owner.PostAsJsonAsync(path, new
            {
                name = "SOC 2",
                plan = new
                {
                    target_readiness_date = (string?)null,
                    target_type_i_as_of_date = (string?)null,
                    target_type_ii_start_date = (string?)null,
                    target_type_ii_end_date = (string?)null,
                    readiness_advisor = (string?)null,
                    audit_firm = (string?)null,
                },
            });
            if (program.StatusCode == HttpStatusCode.OK)
            {
                var programId = Guid.Parse((await ReadAsync(program))
                    .GetProperty("program_id").GetString()!);
                return programId;
            }
            Assert.True(program.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await program.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation never became authorized after tenant bootstrap.");
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
        throw new TimeoutException("The risk draft projection did not catch up.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
