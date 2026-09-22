using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ControlDraftE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldPreserveDraftHistoryAndDenyDisclosureGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"control-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"control-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/controls";
        var content = Content("Review access quarterly");
        using var emptyResponse = await owner.GetAsync(path);
        var empty = await ReadAsync(emptyResponse);

        // Act
        using var createdResponse = await owner.PostAsJsonAsync(path, new
        {
            identifier = " ac-01 ",
            content,
        });
        var registration = await ReadAsync(createdResponse);
        var controlId = registration.GetProperty("control_id").GetString();
        var draftPath = $"{path}/{controlId}/draft";
        using var duplicate = await owner.PostAsJsonAsync(path, new
        {
            identifier = "AC-01",
            content,
        });
        using var changedDuplicate = await owner.PostAsJsonAsync(path, new
        {
            identifier = "AC-01",
            content = Content("Different control"),
        });
        var original = await WaitForRevisionAsync(owner, draftPath, 1);
        using var revisedResponse = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            content = Content("Review access monthly"),
        });
        using var stale = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            content = Content("Stale edit"),
        });
        var revised = await WaitForRevisionAsync(owner, draftPath, 2);
        using var revisionOne = await owner.GetAsync($"{draftPath}/revisions/1");
        using var revisionTwo = await owner.GetAsync($"{draftPath}/revisions/2");
        var historyPath = $"{draftPath}/revisions";
        var firstHistory = await WaitForHistoryPageAsync(owner,
            $"{historyPath}?limit=1&minimum_control_draft_revision=2");
        var historyCursor = firstHistory.GetProperty("next_cursor").GetString();
        Assert.NotNull(historyCursor);
        var secondHistory = await WaitForHistoryPageAsync(owner,
            $"{historyPath}?limit=1&cursor={Uri.EscapeDataString(historyCursor!)}");
        using var futureHistory = await owner.GetAsync(
            $"{historyPath}?minimum_control_draft_revision=3");
        using var invalidMinimumHistory = await owner.GetAsync(
            $"{historyPath}?minimum_control_draft_revision=0");
        using var malformedHistoryCursor = await owner.GetAsync($"{historyPath}?cursor=not-a-cursor");
        using var listedResponse = await owner.GetAsync(path);
        var listed = await ReadAsync(listedResponse);
        using var outsiderRead = await outsider.GetAsync(draftPath);
        using var outsiderList = await outsider.GetAsync(path);
        using var outsiderHistory = await outsider.GetAsync(historyPath);
        using var outsiderRevise = await outsider.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 2,
            content = Content("Unauthorized edit"),
        });
        using var otherProgram = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{Guid.NewGuid()}/controls/{controlId}/draft");
        var otherTenant = await CreateProgramAsync(owner);
        using var otherTenantRead = await owner.GetAsync(
            $"/api/v1/tenants/{otherTenant.TenantId}/programs/{otherTenant.ProgramId}/controls/{controlId}/draft");
        using var otherTenantHistory = await owner.GetAsync(
            $"/api/v1/tenants/{otherTenant.TenantId}/programs/{otherTenant.ProgramId}/controls/{controlId}/draft/revisions");
        using var oversized = await owner.PostAsJsonAsync(path, new
        {
            identifier = "OVERSIZED-01",
            content = new
            {
                title = "Oversized draft",
                objective = "Review access",
                description = "A bounded draft",
                implementation_narrative = new string('n', 12000),
                expected_evidence_descriptions = Enumerable.Repeat(new string('e', 2000), 20)
                    .ToArray(),
            },
        });
        var controlEvents = new List<DomainEvent>();
        var store = factory.Services.GetRequiredService<IEventStore>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), "controls"),
                           EventCursor.Start, CancellationToken.None))
            controlEvents.Add(record.Event);

        // Assert
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        Assert.Equal("AC-01", registration.GetProperty("identifier").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changedDuplicate.StatusCode);
        Assert.Equal("draft", original.GetProperty("status").GetString());
        Assert.Equal("unresolved", original.GetProperty("owner_resolution").GetString());
        Assert.Equal("unresolved", original.GetProperty("applicability_resolution").GetString());
        Assert.Equal(HttpStatusCode.NoContent, revisedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Review access monthly", revised.GetProperty("content")
            .GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, revisionOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revisionTwo.StatusCode);
        Assert.Equal("Review access quarterly", (await ReadAsync(revisionOne))
            .GetProperty("content").GetProperty("title").GetString());
        Assert.Equal("Review access monthly", (await ReadAsync(revisionTwo))
            .GetProperty("content").GetProperty("title").GetString());
        Assert.Equal([1L], firstHistory.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("revision").GetInt64()));
        Assert.Equal("Review access quarterly", firstHistory.GetProperty("items")[0]
            .GetProperty("content").GetProperty("title").GetString());
        Assert.Equal([2L], secondHistory.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("revision").GetInt64()));
        Assert.Equal("Review access monthly", secondHistory.GetProperty("items")[0]
            .GetProperty("content").GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Conflict, futureHistory.StatusCode);
        Assert.Equal("true", futureHistory.Headers.GetValues("Portia-Transient").Single());
        Assert.Equal(HttpStatusCode.BadRequest, invalidMinimumHistory.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedHistoryCursor.StatusCode);
        Assert.Single(listed.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderHistory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRevise.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherProgram.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherTenantRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherTenantHistory.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal(2, controlEvents.Count);
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            var mcpControlId = ControlDraft.IdFor(Uuid.FromGuid(tenantId),
                Uuid.FromGuid(programId), "MCP-01");
            _ = await mcp.When("bdgrz.control.draft.create", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["identifier"] = "MCP-01",
                ["content"] = Content("Review access through MCP"),
            }).ExpectSuccess();
            _ = await WaitForRevisionAsync(owner, $"{path}/{mcpControlId}/draft", 1);
            _ = await mcp.When("bdgrz.control.draft.revise", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["control_id"] = mcpControlId,
                ["expected_revision"] = 1,
                ["content"] = Content("Review access through MCP monthly"),
            }).ExpectSuccess();
            var mcpRevised = await WaitForRevisionAsync(owner, $"{path}/{mcpControlId}/draft", 2);
            Assert.Equal("Review access through MCP monthly", mcpRevised.GetProperty("content")
                .GetProperty("title").GetString());
            _ = await mcp.When("bdgrz.control.draft.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["control_id"] = controlId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.control.draft.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.control.draft.revision.get",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["control_id"] = controlId,
                    ["revision"] = 1,
                }).ExpectSuccess();
            _ = await mcp.When("bdgrz.control.draft.revisions.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["control_id"] = controlId,
                    ["limit"] = 1,
                    ["minimum_control_draft_revision"] = 2,
                }).ExpectSuccess();
            var futureHistoryMcp = await mcp.When("bdgrz.control.draft.revisions.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["control_id"] = controlId,
                    ["minimum_control_draft_revision"] = 3,
                }).ExpectFailure("Conflict");
            Assert.True(Assert.IsType<JsonElement>(futureHistoryMcp.StructuredJson)
                .GetProperty("isTransient").GetBoolean());
            var invalidMinimumHistoryMcp = await mcp.When("bdgrz.control.draft.revisions.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["control_id"] = controlId,
                    ["minimum_control_draft_revision"] = 0,
                }).ExpectFailure("Validation");
            Assert.False(Assert.IsType<JsonElement>(invalidMinimumHistoryMcp.StructuredJson)
                .GetProperty("isTransient").GetBoolean());
        }
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.control.draft.create", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["identifier"] = "MCP-NO-ACCESS",
                ["content"] = Content("Unauthorized MCP control"),
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.control.draft.revise", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["control_id"] = controlId,
                ["expected_revision"] = 2,
                ["content"] = Content("Unauthorized MCP revision"),
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.control.draft.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["control_id"] = controlId,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.control.draft.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.control.draft.revision.get",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["control_id"] = controlId,
                    ["revision"] = 1,
                }).ExpectFailure();
            _ = await mcp.When("bdgrz.control.draft.revisions.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["control_id"] = controlId,
                }).ExpectFailure();
        }
        using var openApiResponse = await owner.GetAsync("/openapi/v1.json");
        var openApi = await ReadAsync(openApiResponse);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var paths = openApi.GetProperty("paths");
        var collection = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls");
        var createSchema = collection.GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.True(createSchema.GetProperty("properties").TryGetProperty("identifier", out _));
        Assert.True(createSchema.GetProperty("properties").TryGetProperty("content", out _));
        Assert.False(createSchema.GetProperty("properties").TryGetProperty("tenant_id", out _));
        Assert.True(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft")
            .TryGetProperty("put", out _));
        var historyOperation = paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions")
            .GetProperty("get");
        Assert.True(historyOperation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(historyOperation.GetProperty("responses").TryGetProperty("400", out _));
        foreach (var name in new[] { "limit", "cursor", "minimum_control_draft_revision" })
            Assert.Contains(historyOperation.GetProperty("parameters").EnumerateArray(), parameter =>
                parameter.GetProperty("name").GetString() == name &&
                parameter.GetProperty("in").GetString() == "query");
        using var secondControl = await owner.PostAsJsonAsync(path, new
        {
            identifier = "AC-02",
            content = Content("Review vendor access"),
        });
        Assert.Equal(HttpStatusCode.OK, secondControl.StatusCode);
        var secondControlId = (await ReadAsync(secondControl)).GetProperty("control_id").GetString();
        _ = await WaitForRevisionAsync(owner, $"{path}/{secondControlId}/draft", 1);
        var secondHistoryPath = $"{path}/{secondControlId}/draft/revisions";
        _ = await WaitForHistoryPageAsync(owner,
            $"{secondHistoryPath}?minimum_control_draft_revision=1");
        using var crossControlHistoryCursor = await owner.GetAsync(
            $"{secondHistoryPath}?cursor={Uri.EscapeDataString(historyCursor!)}");
        using var firstPageResponse = await owner.GetAsync($"{path}?limit=1");
        var firstPage = await ReadAsync(firstPageResponse);
        var cursor = firstPage.GetProperty("next_cursor").GetString();
        Assert.NotNull(cursor);
        using var malformedCursor = await owner.GetAsync($"{path}?cursor=not-a-cursor");
        using var invalidLimit = await owner.GetAsync($"{path}?limit=201");
        var secondProgramId = await CreateProgramForTenantAsync(owner, tenantId);
        using var crossProgramCursor = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{secondProgramId}/controls?cursor={Uri.EscapeDataString(cursor)}");
        using var crossProgramHistory = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{secondProgramId}/controls/{controlId}/draft/revisions");
        Assert.Equal(HttpStatusCode.BadRequest, malformedCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossProgramCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossControlHistoryCursor.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossProgramHistory.StatusCode);
    }

    [Fact]
    public async Task ShouldRecoverDraftProjectionGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-control-split-{Guid.NewGuid():N}";
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
            $"control-split-owner-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/controls";
        await worker.StopAsync();

        // Act
        using var created = await owner.PostAsJsonAsync(path, new
        {
            identifier = "CC-01",
            content = Content("Review changes"),
        });
        var controlId = (await ReadAsync(created)).GetProperty("control_id").GetString();
        var draftPath = $"{path}/{controlId}/draft";
        var historyPath = $"{draftPath}/revisions";
        using var lagged = await owner.GetAsync($"{draftPath}?minimum_revision=1");
        using var laggedList = await owner.GetAsync(path);
        using var laggedHistory = await owner.GetAsync(
            $"{historyPath}?minimum_control_draft_revision=1");
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var projected = await WaitForRevisionAsync(owner, draftPath, 1);
            using var exact = await owner.GetAsync($"{draftPath}/revisions/1");
            using var listed = await owner.GetAsync(path);
            var history = await WaitForHistoryPageAsync(owner,
                $"{historyPath}?minimum_control_draft_revision=1");

            // Assert
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, laggedList.StatusCode);
            Assert.Equal("true", laggedList.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, laggedHistory.StatusCode);
            Assert.Equal("true", laggedHistory.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("draft", projected.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            Assert.Single((await ReadAsync(listed)).GetProperty("items").EnumerateArray());
            Assert.Equal([1L], history.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("revision").GetInt64()));
            await restarted.StopAsync();
            using var revisedResponse = await owner.PutAsJsonAsync(draftPath, new
            {
                expected_revision = 1,
                content = Content("Review changes monthly"),
            });
            using var staleCurrent = await owner.GetAsync(draftPath);
            using var staleList = await owner.GetAsync(path);
            using var staleHistory = await owner.GetAsync(
                $"{historyPath}?minimum_control_draft_revision=2");
            Assert.Equal(HttpStatusCode.NoContent, revisedResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, staleCurrent.StatusCode);
            Assert.Equal("true", staleCurrent.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, staleList.StatusCode);
            Assert.Equal("true", staleList.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, staleHistory.StatusCode);
            Assert.Equal("true", staleHistory.Headers.GetValues("Portia-Transient").Single());
            using var resumed = BuildWorker(applicationName);
            await resumed.StartAsync();
            try
            {
                var latest = await WaitForRevisionAsync(owner, draftPath, 2);
                var recoveredHistory = await WaitForHistoryPageAsync(owner,
                    $"{historyPath}?minimum_control_draft_revision=2");
                Assert.Equal("Review changes monthly", latest.GetProperty("content")
                    .GetProperty("title").GetString());
                Assert.Equal([1L, 2L], recoveredHistory.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("revision").GetInt64()));
                Assert.Equal("Review changes", recoveredHistory.GetProperty("items")[0]
                    .GetProperty("content").GetProperty("title").GetString());
                Assert.Equal("Review changes monthly", recoveredHistory.GetProperty("items")[1]
                    .GetProperty("content").GetProperty("title").GetString());
            }
            finally
            {
                await resumed.StopAsync();
            }
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

    static object Content(string title) => new
    {
        title,
        objective = "Ensure access is reviewed",
        description = "People with privileged access are reviewed.",
        implementation_narrative = "The security lead reviews the access listing.",
        expected_evidence_descriptions = new[] { "Review record", "Access listing" },
    };

    static async Task<(Guid TenantId, Guid ProgramId)> CreateProgramAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Control draft tenant",
            slug = $"control-{Guid.NewGuid():N}"[..24],
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
        throw new TimeoutException("The control draft projection did not catch up.");
    }

    static async Task<JsonElement> WaitForHistoryPageAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("true", response.Headers.GetValues("Portia-Transient").Single());
            await Task.Delay(250);
        }
        throw new TimeoutException("The control draft history projection did not catch up.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
