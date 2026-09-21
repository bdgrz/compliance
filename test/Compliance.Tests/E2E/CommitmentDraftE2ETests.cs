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
public sealed class CommitmentDraftE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldPreserveFourDraftKindsAndDenyDisclosureGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"commitment-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"commitment-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId, serviceId) = await CreateServiceAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/commitment_drafts";
        using var empty = await owner.GetAsync(path);

        // Act
        using var created = await owner.PostAsJsonAsync(path, new
        {
            service_id = serviceId,
            kind = "service_commitment",
            identifier = " sc-01 ",
            statement = "Management's draft service promise",
            context = "Not reviewed",
            source_reference = "Contract section 4",
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var registration = await ReadAsync(created);
        var draftId = registration.GetProperty("draft_id").GetString();
        var draftPath = $"{path}/{draftId}";
        using var duplicate = await owner.PostAsJsonAsync(path, new
        {
            service_id = serviceId,
            kind = "service_commitment",
            identifier = "SC-01",
            statement = "Management's draft service promise",
            context = "Not reviewed",
            source_reference = "Contract section 4",
        });
        using var missingService = await owner.PostAsJsonAsync(path, new
        {
            service_id = Guid.NewGuid(),
            kind = "system_requirement",
            identifier = "REQ-99",
            statement = "A requirement",
            context = "Draft",
            source_reference = "Policy section 1",
        });
        var otherProgramId = await CreateProgramAsync(owner, tenantId);
        using var wrongProgramService = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/programs/{otherProgramId}/commitment_drafts", new
            {
                service_id = serviceId,
                kind = "system_requirement",
                identifier = "REQ-98",
                statement = "Another requirement",
                context = "Draft",
                source_reference = "Policy section 2",
            });
        var first = await WaitForRevisionAsync(owner, draftPath, 1);
        using var revised = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            statement = "Revised draft promise",
            context = "Still unreviewed",
            source_reference = "Contract section 5",
        });
        using var stale = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            statement = "Stale draft",
            context = "",
            source_reference = "Contract section 6",
        });
        var second = await WaitForRevisionAsync(owner, draftPath, 2);
        using var historical = await owner.GetAsync($"{draftPath}/revisions/1");
        using var otherProgram = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{Guid.NewGuid()}/commitment_drafts/{draftId}");
        var (otherTenantId, otherTenantProgramId, _) = await CreateServiceAsync(owner);
        using var crossTenantRead = await owner.GetAsync(
            $"/api/v1/tenants/{otherTenantId}/programs/{otherTenantProgramId}/commitment_drafts/{draftId}");
        using var outsiderRead = await outsider.GetAsync(draftPath);
        using var outsiderList = await outsider.GetAsync(path);
        using var outsiderWrite = await outsider.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 2,
            statement = "Unauthorized",
            context = "",
            source_reference = "Other",
        });
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            foreach (var kind in new[] { "system_requirement", "user_entity_responsibility",
                         "subservice_responsibility" })
            {
                _ = await mcp.When("bdgrz.commitment.draft.create", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["service_id"] = serviceId,
                    ["kind"] = kind,
                    ["identifier"] = $"{kind.ToUpperInvariant()}-01",
                    ["statement"] = $"Draft {kind}",
                    ["context"] = "Not reviewed",
                    ["source_reference"] = "Management note",
                }).ExpectSuccess();
            }
            _ = await mcp.When("bdgrz.commitment.draft.revise", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["draft_id"] = draftId,
                ["expected_revision"] = 2,
                ["statement"] = "MCP revised draft promise",
                ["context"] = "Still unreviewed",
                ["source_reference"] = "Contract section 5",
            }).ExpectSuccess();
            _ = await WaitForRevisionAsync(owner, draftPath, 3);
            _ = await WaitForListCountAsync(owner, path, 4);
            _ = await mcp.When("bdgrz.commitment.draft.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["draft_id"] = draftId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.commitment.draft.revision.get",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["draft_id"] = draftId,
                    ["revision"] = 1,
                }).ExpectSuccess();
            _ = await mcp.When("bdgrz.commitment.draft.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
            }).ExpectSuccess();
        }
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.commitment.draft.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["draft_id"] = draftId,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.commitment.draft.create",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["service_id"] = serviceId,
                    ["kind"] = "service_commitment",
                    ["identifier"] = "NO-ACCESS",
                    ["statement"] = "Unauthorized",
                    ["context"] = "",
                    ["source_reference"] = "Other",
                }).ExpectFailure();
        }
        using var listedResponse = await owner.GetAsync(path);
        var listed = await ReadAsync(listedResponse);
        using var firstPageResponse = await owner.GetAsync($"{path}?limit=1");
        var firstPage = await ReadAsync(firstPageResponse);
        var cursor = firstPage.GetProperty("next_cursor").GetString();
        Assert.NotNull(cursor);
        using var malformedCursor = await owner.GetAsync($"{path}?cursor=not-a-cursor");
        using var invalidLimit = await owner.GetAsync($"{path}?limit=201");
        using var crossProgramCursor = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{otherProgramId}/commitment_drafts?cursor={Uri.EscapeDataString(cursor)}");
        using var oversized = await owner.PostAsJsonAsync(path, new
        {
            service_id = serviceId,
            kind = "service_commitment",
            identifier = "OVERSIZED-01",
            statement = new string('界', 16000),
            context = new string('界', 16000),
            source_reference = "Management note",
        });
        var draftEvents = new List<DomainEvent>();
        var store = factory.Services.GetRequiredService<IEventStore>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(),
                               "commitment-drafts"), EventCursor.Start,
                           CancellationToken.None))
            draftEvents.Add(record.Event);
        using var openApiResponse = await owner.GetAsync("/openapi/v1.json");
        var openApi = await ReadAsync(openApiResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingService.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongProgramService.StatusCode);
        Assert.Equal("draft", first.GetProperty("status").GetString());
        Assert.Equal("unverified", first.GetProperty("source_resolution").GetString());
        Assert.Equal("unresolved", first.GetProperty("applicability_resolution").GetString());
        Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Revised draft promise", second.GetProperty("statement").GetString());
        Assert.Equal("Contract section 4", (await ReadAsync(historical))
            .GetProperty("source_reference").GetString());
        Assert.Equal(HttpStatusCode.NotFound, otherProgram.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderWrite.StatusCode);
        Assert.Equal(4, listed.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, malformedCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossProgramCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal(6, draftEvents.Count);
        var collection = openApi.GetProperty("paths").GetProperty(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment_drafts");
        Assert.True(collection.TryGetProperty("post", out _));
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(collection.GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("properties").TryGetProperty("source_reference", out _));
    }

    [Fact]
    public async Task ShouldRecoverProjectionGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-commitment-split-{Guid.NewGuid():N}";
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
            $"commitment-split-owner-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId, serviceId) = await CreateServiceAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/commitment_drafts";
        await worker.StopAsync();

        // Act
        using var created = await owner.PostAsJsonAsync(path, new
        {
            service_id = serviceId,
            kind = "service_commitment",
            identifier = "SC-02",
            statement = "Draft",
            context = "",
            source_reference = "Management note",
        });
        var draftId = (await ReadAsync(created)).GetProperty("draft_id").GetString();
        var draftPath = $"{path}/{draftId}";
        using var lagged = await owner.GetAsync($"{draftPath}?minimum_revision=1");
        using var laggedList = await owner.GetAsync(path);
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var projected = await WaitForRevisionAsync(owner, draftPath, 1);
            using var exact = await owner.GetAsync($"{draftPath}/revisions/1");

            // Assert
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, laggedList.StatusCode);
            Assert.Equal("true", laggedList.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("draft", projected.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
        }
        finally
        {
            await restarted.StopAsync();
        }
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

    static async Task<(Guid TenantId, Guid ProgramId, Guid ServiceId)> CreateServiceAsync(
        HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Commitment draft tenant",
            slug = $"commitment-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        var tenantId = Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
        var programId = await CreateProgramAsync(owner, tenantId);
        using var service = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/programs/{programId}/client-services", new
            {
                name = "Client service",
                purpose = "Provide the contracted service",
                owner_reference = "Operations",
            });
        Assert.Equal(HttpStatusCode.OK, service.StatusCode);
        var serviceId = Guid.Parse((await ReadAsync(service)).GetProperty("service_id").GetString()!);
        return (tenantId, programId, serviceId);
    }

    static async Task<Guid> CreateProgramAsync(HttpClient owner, Guid tenantId)
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
                return Guid.Parse((await ReadAsync(program)).GetProperty("program_id").GetString()!);
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
        throw new TimeoutException("The commitment draft projection did not catch up.");
    }

    static async Task<JsonElement> WaitForListCountAsync(HttpClient client, string path,
        int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var list = await ReadAsync(response);
                if (list.GetProperty("items").GetArrayLength() == count)
                    return list;
            }
            else
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("The commitment draft list projection did not catch up.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
